using System.Data;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Tasks.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Plans.Services;

public sealed class PlanCommandService(RobotMissionDbContext db, TimeProvider? timeProvider = null) : IPlanCommandService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<PlanCommandResult> CreateFallbackAsync(Guid userId, string floor, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(floor)) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var zone = await db.Zones.FromSqlInterpolated($"SELECT * FROM public.\"Zones\" WHERE \"Code\" = {floor.Trim()} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (zone is null) return PlanCommandResult.Failure(PlanCommandError.FloorNotFound);
        if (!zone.IsActive) return PlanCommandResult.Failure(PlanCommandError.FloorInactive);

        var now = clock.GetUtcNow().UtcDateTime;
        var previous = await db.DispatchPlans
            .Where(plan => plan.ZoneId == zone.Id && plan.Status == DispatchPlanStatus.Draft)
            .SingleOrDefaultAsync(cancellationToken);
        if (previous is not null) { previous.Status = DispatchPlanStatus.Discarded; previous.DiscardedAt = now; }

        var map = await db.MapVersions.AsNoTracking().SingleOrDefaultAsync(version => version.ZoneId == zone.Id && version.Status == MapVersionStatus.Published, cancellationToken);
        QueuePrioritySettings settings;
        try { settings = await QueuePriority.LoadAsync(db, cancellationToken); }
        catch (InvalidOperationException) { return PlanCommandResult.Failure(PlanCommandError.QueueConfigurationInvalid); }
        var missions = await db.Missions.AsNoTracking()
            .Include(mission => mission.MissionType)
            .Where(mission => mission.ZoneId == zone.Id && (mission.Status == MissionStatus.Pending || mission.Status == MissionStatus.Rescheduled))
            .ToListAsync(cancellationToken);
        var robots = await db.Robots.AsNoTracking().Include(robot => robot.RobotType)
            .Where(robot => robot.ZoneId == zone.Id && robot.State == RobotState.Available && robot.IsConnected)
            .OrderBy(robot => robot.RobotCode).ToListAsync(cancellationToken);

        var plan = new DispatchPlan { Id = Guid.NewGuid(), ZoneId = zone.Id, Status = DispatchPlanStatus.Draft, Source = DispatchPlanSource.Fallback,
            Reason = "Fallback priority dispatch: one mission per available robot.", CreatedByUserId = userId, CreatedAt = now, Version = 1 };
        db.DispatchPlans.Add(plan);

        var available = robots.ToList();
        var sequence = 1;
        foreach (var mission in missions.OrderByDescending(mission => QueuePriority.Score(mission, settings, now)).ThenBy(mission => mission.CreatedAt).ThenBy(mission => mission.MissionCode))
        {
            if (map is null) { AddWaiting(plan, mission, "MAP_NOT_PUBLISHED"); continue; }
            if (mission.FromLocationId is null) { AddWaiting(plan, mission, "PICKUP_LOCATION_NOT_CONFIGURED"); continue; }
            if (mission.ToLocationId is null) { AddWaiting(plan, mission, "DELIVERY_LOCATION_NOT_CONFIGURED"); continue; }
            var robot = available.FirstOrDefault(candidate => candidate.RobotTypeId == mission.MissionType.RobotTypeId && candidate.RobotType.Slots > 0);
            if (robot is null) { AddWaiting(plan, mission, "NO_AVAILABLE_ROBOT"); continue; }
            available.Remove(robot);
            var trip = new DispatchPlanTrip { Id = Guid.NewGuid(), PlanId = plan.Id, RobotId = robot.Id, MapVersionId = map.Id, PickupLocationId = mission.FromLocationId, Seq = sequence++ };
            var pickup = new DispatchPlanStop { Id = Guid.NewGuid(), PlanTripId = trip.Id, Seq = 1, LocationId = mission.FromLocationId.Value, Action = TripStopAction.Pickup };
            var delivery = new DispatchPlanStop { Id = Guid.NewGuid(), PlanTripId = trip.Id, Seq = 2, LocationId = mission.ToLocationId.Value, Action = TripStopAction.Drop };
            db.DispatchPlanTrips.Add(trip); db.DispatchPlanStops.AddRange(pickup, delivery);
            db.DispatchPlanTasks.Add(new DispatchPlanTask { Id = Guid.NewGuid(), PlanId = plan.Id, MissionId = mission.Id, PlanTripId = trip.Id, DeliveryPlanStopId = delivery.Id, Status = DispatchPlanTaskStatus.Assigned, SlotNo = 1, Priority = mission.Priority });
        }
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = now, Action = "PLAN_DRAFT_CREATED", TargetObject = $"DispatchPlan:{plan.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return PlanCommandResult.Success(plan.Id);
    }

    private static void AddWaiting(DispatchPlan plan, Mission mission, string reason) => plan.Tasks.Add(new DispatchPlanTask { Id = Guid.NewGuid(), PlanId = plan.Id, MissionId = mission.Id, Status = DispatchPlanTaskStatus.Waiting, Priority = mission.Priority, WaitReason = reason });
}
