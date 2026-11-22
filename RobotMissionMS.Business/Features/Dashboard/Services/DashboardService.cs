using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Dashboard.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Dashboard.Services;

public sealed class DashboardService(RobotMissionDbContext db, TimeProvider clock) : IDashboardService
{
    public async Task<DashboardOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var zone = VietnamTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
        var localStart = localNow.Date;
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart.AddDays(1), DateTimeKind.Unspecified), zone);

        var robotStates = await db.Robots.AsNoTracking().GroupBy(x => x.State)
            .Select(x => new { State = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken);
        var missionStates = await db.Missions.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end).GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var queueCount = await db.Missions.AsNoTracking().CountAsync(x => x.Status == MissionStatus.Pending || x.Status == MissionStatus.Rescheduled, cancellationToken);
        var openIncidentCount = await db.Alerts.AsNoTracking().CountAsync(x => x.Status != AlertStatus.Resolved, cancellationToken);
        var completed = new[] { MissionStatus.Completed, MissionStatus.CompletedByHuman };
        var onTimeRows = await db.Missions.AsNoTracking().Where(x => completed.Contains(x.Status) && x.DeadlineAt != null && x.CompletedAt != null && x.CompletedAt >= start && x.CompletedAt < end)
            .Select(x => new { IsOnTime = x.CompletedAt <= x.DeadlineAt }).ToListAsync(cancellationToken);

        var eligible = onTimeRows.Count;
        var onTime = onTimeRows.Count(x => x.IsOnTime);
        return new DashboardOverviewResponse
        {
            GeneratedAt = now,
            BusinessDate = DateOnly.FromDateTime(localNow),
            RobotCounts = new DashboardRobotCounts
            {
                OnTrip = robotStates.GetValueOrDefault(RobotState.OnTrip), Charging = robotStates.GetValueOrDefault(RobotState.Charging),
                Available = robotStates.GetValueOrDefault(RobotState.Available), Maintenance = robotStates.GetValueOrDefault(RobotState.Maintenance),
                Error = robotStates.GetValueOrDefault(RobotState.Error), Inactive = robotStates.GetValueOrDefault(RobotState.Inactive)
            },
            MissionCounts = new DashboardMissionCounts
            {
                Pending = missionStates.GetValueOrDefault(MissionStatus.Pending), Assigned = missionStates.GetValueOrDefault(MissionStatus.Assigned),
                InProgress = missionStates.GetValueOrDefault(MissionStatus.InProgress), DeliveryFailed = missionStates.GetValueOrDefault(MissionStatus.DeliveryFailed),
                Completed = missionStates.GetValueOrDefault(MissionStatus.Completed), CompletedByHuman = missionStates.GetValueOrDefault(MissionStatus.CompletedByHuman),
                Rescheduled = missionStates.GetValueOrDefault(MissionStatus.Rescheduled), Cancelled = missionStates.GetValueOrDefault(MissionStatus.Cancelled)
            },
            QueueCount = queueCount,
            OpenIncidentCount = openIncidentCount,
            OnTime = new DashboardOnTimeMetrics { Eligible = eligible, OnTime = onTime, Rate = eligible == 0 ? null : Math.Round((decimal)onTime / eligible, 4) }
        };
    }

    private static TimeZoneInfo VietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }
}
