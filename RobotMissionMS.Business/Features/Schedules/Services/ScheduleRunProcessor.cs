using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.Business.Features.ActivityLogs.Services;

namespace RobotMissionMS.Business.Features.Schedules.Services;

/// <summary>Evaluates one persisted schedule run. It is safe to call repeatedly.</summary>
public sealed class ScheduleRunProcessor(
    RobotMissionDbContext db,
    ScheduledRouteDispatchService dispatcher,
    IActivityRealtimePublisher? activityRealtime = null)
{
    public async Task<ScheduledRouteDispatchResult> EvaluateAsync(
        Guid runId,
        int maxWaitMinutes,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var run = await db.MissionScheduleRuns
            .Include(x => x.Schedule)
            .SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);

        if (run is null || run.Status != ScheduleRunStatus.WaitingForRobot)
            return ScheduledRouteDispatchResult.AlreadyCreated;

        var result = await dispatcher.DispatchAsync(run.Id, cancellationToken);
        if (result == ScheduledRouteDispatchResult.Created || result == ScheduledRouteDispatchResult.AlreadyCreated)
            return result;

        await db.Entry(run).ReloadAsync(cancellationToken);
        if (run.Status != ScheduleRunStatus.WaitingForRobot)
            return result;

        run.LastEvaluatedAt = now;
        run.FailureReason = FailureReason(result);

        if (now >= run.ScheduledFor.AddMinutes(maxWaitMinutes))
        {
            run.Status = ScheduleRunStatus.Late;
            run.LateAt = now;

            if (run.LateAlertId is null)
            {
                var alert = new Alert
                {
                    Id = Guid.NewGuid(),
                    AlertType = "SCHEDULE_LATE",
                    ScenarioCode = "E1",
                    ZoneId = run.Schedule.ZoneId,
                    Message = $"Schedule {run.Schedule.Name} waited more than {maxWaitMinutes} minutes.",
                    Category = AlertCategory.System,
                    Severity = AlertSeverity.Warning,
                    Status = AlertStatus.Open,
                    CreatedAt = now
                };
                db.Alerts.Add(alert);
                run.LateAlertId = alert.Id;
                db.ActivityLogs.Add(new ActivityLog { Timestamp = now, Action = "SCHEDULE_RUN_LATE", TargetObject = $"ScheduleRun:{run.Id:D}" });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (run.Status == ScheduleRunStatus.Late && activityRealtime is not null && run.Schedule.ZoneId != Guid.Empty)
        {
            var floor = await db.Zones.AsNoTracking().Where(zone => zone.Id == run.Schedule.ZoneId).Select(zone => zone.Code).SingleOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(floor)) await activityRealtime.PublishAsync(floor, new ActivityRealtimeEvent(now, "SCHEDULE_RUN_LATE", $"ScheduleRun:{run.Id:D}", null), cancellationToken);
        }
        return result;
    }

    private static string FailureReason(ScheduledRouteDispatchResult result) => result switch
    {
        ScheduledRouteDispatchResult.NoEligibleRobot => "NO_ELIGIBLE_ROBOT",
        ScheduledRouteDispatchResult.MapNotPublished => "MAP_NOT_PUBLISHED",
        ScheduledRouteDispatchResult.InvalidSnapshot => "INVALID_ROUTE_SNAPSHOT",
        _ => "DISPATCH_NOT_CREATED"
    };
}
