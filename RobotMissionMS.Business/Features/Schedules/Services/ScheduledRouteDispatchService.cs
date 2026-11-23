using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Tasks.Services;
using RobotMissionMS.Business.Features.ActivityLogs.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Schedules.Services;

public enum ScheduledRouteDispatchResult { Created, NoEligibleRobot, MapNotPublished, InvalidSnapshot, AlreadyCreated }

public sealed class ScheduledRouteDispatchService(RobotMissionDbContext db, IMissionRealtimePublisher? realtime = null, IActivityRealtimePublisher? activityRealtime = null)
{
    public async Task<ScheduledRouteDispatchResult> DispatchAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var run = await db.MissionScheduleRuns.Include(x => x.Schedule).ThenInclude(x => x.MissionType).Include(x => x.Schedule).ThenInclude(x => x.Zone)
            .SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status == ScheduleRunStatus.MissionCreated) return ScheduledRouteDispatchResult.AlreadyCreated;

        Snapshot? snapshot;
        try { snapshot = JsonSerializer.Deserialize<Snapshot>(run.RouteSnapshot, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return ScheduledRouteDispatchResult.InvalidSnapshot; }
        if (snapshot?.Stops is not { Count: > 0 }) return ScheduledRouteDispatchResult.InvalidSnapshot;

        var requiredType = run.Schedule.MissionType.Code == "WASTE_COLLECTION" ? "WASTE_CART" : "CLEAN_BOT";
        var robot = await db.Robots.SingleOrDefaultAsync(x => x.ZoneId == run.Schedule.ZoneId && x.IsConnected
            && x.State == RobotState.Available && x.Battery >= 20 && x.RobotType.TypeCode == requiredType, cancellationToken);
        if (robot is null) return ScheduledRouteDispatchResult.NoEligibleRobot;
        var map = await db.MapVersions.SingleOrDefaultAsync(x => x.ZoneId == run.Schedule.ZoneId && x.Status == MapVersionStatus.Published, cancellationToken);
        if (map is null) return ScheduledRouteDispatchResult.MapNotPublished;
        var codes = snapshot.Stops.Select(x => x.Code).ToList();
        var locations = await db.Locations.Where(x => x.ZoneId == run.Schedule.ZoneId && codes.Contains(x.Code) && x.IsActive).ToListAsync(cancellationToken);
        if (locations.Count != codes.Distinct().Count()) return ScheduledRouteDispatchResult.InvalidSnapshot;

        var now = DateTime.UtcNow; var suffix = run.Id.ToString("N")[..8];
        var mission = new Mission { Id = Guid.NewGuid(), MissionCode = $"SCH-{suffix}", MissionTypeId = run.Schedule.MissionTypeId,
            ZoneId = run.Schedule.ZoneId, ScheduleId = run.ScheduleId, ScheduledFor = run.ScheduledFor, Status = MissionStatus.Assigned,
            Priority = MissionPriority.Normal, CreatedAt = now, Note = "Created from scheduled route." };
        var trip = new Trip { Id = Guid.NewGuid(), TripCode = $"STR-{suffix}", ZoneId = run.Schedule.ZoneId, RobotId = robot.Id,
            MapVersionId = map.Id, Status = TripStatus.Planned, CreatedAt = now };
        foreach (var stop in snapshot.Stops.OrderBy(x => x.Seq))
            trip.TripStops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, Seq = stop.Seq,
                LocationId = locations.Single(x => x.Code == stop.Code).Id, Action = TripStopAction.RoutePoint, Status = TripStopStatus.Pending });
        mission.TripId = trip.Id; robot.State = RobotState.OnTrip; run.MissionId = mission.Id; run.Status = ScheduleRunStatus.MissionCreated;
        run.LastEvaluatedAt = now; run.CompletedAt = now;
        db.Missions.Add(mission); db.Trips.Add(trip);
        db.MissionStatusHistories.Add(new MissionStatusHistory { Id = Guid.NewGuid(), MissionId = mission.Id, ChangedAt = now,
            FromStatus = MissionStatus.Pending, ToStatus = MissionStatus.Assigned, Reason = "Scheduled route trip created." });
        db.ActivityLogs.Add(new ActivityLog { Timestamp = now, Action = "SCHEDULE_RUN_MISSION_CREATED", TargetObject = $"ScheduleRun:{run.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        if (ownsTransaction) await transaction!.CommitAsync(cancellationToken);
        if (ownsTransaction && realtime is not null)
            await realtime.PublishTaskUpdatedAsync(run.Schedule.Zone.Code,
                new MissionRealtimeUpdate(mission.Id, "ASSIGNED", robot.Id, mission.SlotNo, "Scheduled route trip created."), cancellationToken);
        if (ownsTransaction && activityRealtime is not null)
            await activityRealtime.PublishAsync(run.Schedule.Zone.Code, new ActivityRealtimeEvent(now, "SCHEDULE_RUN_MISSION_CREATED", $"ScheduleRun:{run.Id:D}", null), cancellationToken);
        return ScheduledRouteDispatchResult.Created;
    }

    private sealed class Snapshot { public List<Stop>? Stops { get; init; } }
    private sealed class Stop { public int Seq { get; init; } public string Code { get; init; } = ""; }
}
