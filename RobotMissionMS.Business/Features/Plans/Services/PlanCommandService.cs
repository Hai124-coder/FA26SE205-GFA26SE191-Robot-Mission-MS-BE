using System.Data;
using System.Text.Json;
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

    public async Task<PlanCommandResult> ReassignTaskAsync(Guid userId, string floor, Guid taskId, Guid? robotId, long version, CancellationToken cancellationToken = default)
    {
        if (!Valid(userId, floor, version) || taskId == Guid.Empty) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
        await using var transaction = await BeginAsync(cancellationToken);
        var plan = await LockDraftAsync(floor, cancellationToken);
        if (plan is null) return PlanCommandResult.Failure(PlanCommandError.NoCurrentPlan);
        if (plan.Version != version) return PlanCommandResult.Failure(PlanCommandError.VersionConflict);
        var task = plan.Tasks.SingleOrDefault(value => value.Id == taskId);
        if (task is null) return PlanCommandResult.Failure(PlanCommandError.TaskNotFound);
        var oldTrip = task.PlanTrip;
        if (robotId is null)
        {
            SetWaiting(task, "MANUALLY_UNASSIGNED");
            RemoveEmptyTrip(plan, oldTrip);
        }
        else
        {
            var robot = await db.Robots.Include(value => value.RobotType).SingleOrDefaultAsync(value => value.Id == robotId, cancellationToken);
            if (robot is null || robot.ZoneId != plan.ZoneId) return PlanCommandResult.Failure(PlanCommandError.FloorForbidden);
            if (robot.State != RobotState.Available || !robot.IsConnected) return PlanCommandResult.Failure(PlanCommandError.RobotUnavailable);
            if (robot.RobotTypeId != task.Mission.MissionType.RobotTypeId) return PlanCommandResult.Failure(PlanCommandError.WrongRobotType);
            if (task.Mission.FromLocationId is null || task.Mission.ToLocationId is null) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
            var target = plan.Trips.SingleOrDefault(value => value.RobotId == robotId);
            if (target is null)
            {
                var map = await db.MapVersions.AsNoTracking().SingleOrDefaultAsync(value => value.ZoneId == plan.ZoneId && value.Status == MapVersionStatus.Published, cancellationToken);
                if (map is null) return PlanCommandResult.Failure(PlanCommandError.MapNotPublished);
                target = new DispatchPlanTrip { Id = Guid.NewGuid(), PlanId = plan.Id, RobotId = robot.Id, MapVersionId = map.Id, PickupLocationId = task.Mission.FromLocationId, Seq = plan.Trips.Count == 0 ? 1 : plan.Trips.Max(value => value.Seq) + 1 };
                var pickup = new DispatchPlanStop { Id = Guid.NewGuid(), PlanTripId = target.Id, Seq = 1, LocationId = task.Mission.FromLocationId.Value, Action = TripStopAction.Pickup };
                var drop = new DispatchPlanStop { Id = Guid.NewGuid(), PlanTripId = target.Id, Seq = 2, LocationId = task.Mission.ToLocationId.Value, Action = TripStopAction.Drop };
                target.Stops.Add(pickup); target.Stops.Add(drop); plan.Trips.Add(target); db.DispatchPlanTrips.Add(target); db.DispatchPlanStops.AddRange(pickup, drop);
            }
            else
            {
                if (target.PickupLocationId != task.Mission.FromLocationId) return PlanCommandResult.Failure(PlanCommandError.PickupMismatch);
                if (target.Tasks.Where(value => value.Id != task.Id && value.Status == DispatchPlanTaskStatus.Assigned).Count() >= robot.RobotType.Slots) return PlanCommandResult.Failure(PlanCommandError.SlotFull);
            }
            var delivery = target.Stops.FirstOrDefault(stop => stop.Action == TripStopAction.Drop && stop.LocationId == task.Mission.ToLocationId);
            if (delivery is null)
            {
                delivery = new DispatchPlanStop { Id = Guid.NewGuid(), PlanTripId = target.Id, Seq = target.Stops.Max(stop => stop.Seq) + 1, LocationId = task.Mission.ToLocationId.Value, Action = TripStopAction.Drop };
                target.Stops.Add(delivery); db.DispatchPlanStops.Add(delivery);
            }
            var used = target.Tasks.Where(value => value.Id != task.Id && value.SlotNo is not null).Select(value => value.SlotNo!.Value).ToHashSet();
            var slot = Enumerable.Range(1, robot.RobotType.Slots).FirstOrDefault(value => !used.Contains(value));
            if (slot == 0) return PlanCommandResult.Failure(PlanCommandError.SlotFull);
            task.Status = DispatchPlanTaskStatus.Assigned; task.PlanTripId = target.Id; task.PlanTrip = target; task.DeliveryPlanStopId = delivery.Id; task.DeliveryPlanStop = delivery; task.SlotNo = slot; task.WaitReason = null;
            if (oldTrip is not null && oldTrip.Id != target.Id) RemoveEmptyTrip(plan, oldTrip);
        }
        Touch(plan, userId, "PLAN_TASK_REASSIGNED");
        await db.SaveChangesAsync(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return PlanCommandResult.Success(plan.Id);
    }

    public async Task<PlanCommandResult> ChangeTaskPriorityAsync(Guid userId, string floor, Guid taskId, string priority, long version, CancellationToken cancellationToken = default)
    {
        if (!Valid(userId, floor, version) || taskId == Guid.Empty || !Enum.TryParse<MissionPriority>(priority, true, out var value) || !Enum.IsDefined(value)) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
        await using var transaction = await BeginAsync(cancellationToken); var plan = await LockDraftAsync(floor, cancellationToken);
        if (plan is null) return PlanCommandResult.Failure(PlanCommandError.NoCurrentPlan); if (plan.Version != version) return PlanCommandResult.Failure(PlanCommandError.VersionConflict);
        var task = plan.Tasks.SingleOrDefault(item => item.Id == taskId); if (task is null) return PlanCommandResult.Failure(PlanCommandError.TaskNotFound);
        task.Priority = value; Touch(plan, userId, "PLAN_TASK_PRIORITY_CHANGED"); await db.SaveChangesAsync(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken); return PlanCommandResult.Success(plan.Id);
    }

    public async Task<PlanCommandResult> RemoveTripAsync(Guid userId, string floor, Guid robotId, long version, CancellationToken cancellationToken = default)
    {
        if (!Valid(userId, floor, version) || robotId == Guid.Empty) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
        await using var transaction = await BeginAsync(cancellationToken); var plan = await LockDraftAsync(floor, cancellationToken);
        if (plan is null) return PlanCommandResult.Failure(PlanCommandError.NoCurrentPlan); if (plan.Version != version) return PlanCommandResult.Failure(PlanCommandError.VersionConflict);
        var trip = plan.Trips.SingleOrDefault(item => item.RobotId == robotId); if (trip is null) return PlanCommandResult.Failure(PlanCommandError.TaskNotFound);
        foreach (var task in trip.Tasks.ToList()) SetWaiting(task, "TRIP_REMOVED_MANUALLY"); RemoveEmptyTrip(plan, trip); Touch(plan, userId, "PLAN_TRIP_REMOVED");
        await db.SaveChangesAsync(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken); return PlanCommandResult.Success(plan.Id);
    }

    public async Task<PlanCommandResult> DiscardCurrentAsync(Guid userId, string floor, long version, CancellationToken cancellationToken = default)
    {
        if (!Valid(userId, floor, version)) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
        await using var transaction = await BeginAsync(cancellationToken); var plan = await LockDraftAsync(floor, cancellationToken);
        if (plan is null) return PlanCommandResult.Failure(PlanCommandError.NoCurrentPlan); if (plan.Version != version) return PlanCommandResult.Failure(PlanCommandError.VersionConflict);
        plan.Status = DispatchPlanStatus.Discarded; plan.DiscardedAt = clock.GetUtcNow().UtcDateTime; Touch(plan, userId, "PLAN_DRAFT_DISCARDED");
        await db.SaveChangesAsync(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken); return PlanCommandResult.Success(plan.Id);
    }

    public async Task<PlanCommandResult> ReleaseCurrentAsync(Guid userId, string floor, long version, CancellationToken cancellationToken = default)
    {
        if (!Valid(userId, floor, version)) return PlanCommandResult.Failure(PlanCommandError.InvalidRequest);
        await using var transaction = await BeginAsync(cancellationToken); var plan = await LockDraftAsync(floor, cancellationToken);
        if (plan is null) return PlanCommandResult.Failure(PlanCommandError.NoCurrentPlan); if (plan.Version != version) return PlanCommandResult.Failure(PlanCommandError.VersionConflict);
        var assignments = plan.Tasks.Where(task => task.Status == DispatchPlanTaskStatus.Assigned && task.PlanTrip is not null).ToList();
        if (assignments.Count == 0) return PlanCommandResult.Failure(PlanCommandError.NoAssignedTasks);
        var now = clock.GetUtcNow().UtcDateTime;
        var locationIds = assignments.SelectMany(task => task.PlanTrip!.Stops).Select(stop => stop.LocationId).Distinct().ToArray();
        var locations = await db.Locations.AsNoTracking().Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, location => location.MapNodeCode, cancellationToken);
        foreach (var planTrip in assignments.Select(task => task.PlanTrip!).DistinctBy(trip => trip.Id))
        {
            var robot = await db.Robots.FromSqlInterpolated($"SELECT * FROM public.\"Robots\" WHERE \"Id\" = {planTrip.RobotId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (robot is null || robot.ZoneId != plan.ZoneId || robot.State != RobotState.Available || !robot.IsConnected || robot.Battery < 20) return PlanCommandResult.Failure(PlanCommandError.PlanStale);
            var tasks = assignments.Where(task => task.PlanTripId == planTrip.Id).ToList();
            if (tasks.Any(task => task.Mission.Status is not MissionStatus.Pending and not MissionStatus.Rescheduled || task.Mission.TripId is not null)) return PlanCommandResult.Failure(PlanCommandError.PlanStale);
            var trip = new Trip { Id = Guid.NewGuid(), TripCode = $"TRP-{Guid.NewGuid():N}"[..36].ToUpperInvariant(), ZoneId = plan.ZoneId, RobotId = robot.Id, MapVersionId = planTrip.MapVersionId, DispatchedByUserId = userId, CreatedAt = now, DispatchedAt = now, Status = TripStatus.Dispatched };
            db.Trips.Add(trip);
            foreach (var stop in planTrip.Stops.OrderBy(stop => stop.Seq)) db.TripStops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, Seq = stop.Seq, LocationId = stop.LocationId, Action = stop.Action, Status = TripStopStatus.Pending });
            foreach (var task in tasks)
            {
                var mission = task.Mission; var from = mission.Status; mission.TripId = trip.Id; mission.SlotNo = task.SlotNo; mission.Status = MissionStatus.Assigned;
                db.MissionStatusHistories.Add(new MissionStatusHistory { Id = Guid.NewGuid(), MissionId = mission.Id, ChangedAt = now, ChangedByUserId = userId, FromStatus = from, ToStatus = MissionStatus.Assigned, Reason = $"Released from dispatch plan {plan.Id:D}." });
            }
            robot.State = RobotState.OnTrip;
            db.RobotCommandOutbox.Add(new RobotCommandOutbox
            {
                Id = Guid.NewGuid(), CommandType = "DISPATCH_TRIP", RobotId = robot.Id, TripId = trip.Id, PlanId = plan.Id,
                Topic = $"rmms/v1/{robot.RobotCode}/order", PayloadJson = BuildDispatchPayload(trip, robot, planTrip, tasks, locations),
                Status = RobotCommandOutboxStatus.Pending, AttemptCount = 0, NextAttemptAt = now, CreatedAt = now, UpdatedAt = now
            });
        }
        plan.Status = DispatchPlanStatus.Released; plan.ReleasedAt = now; plan.ReleasedByUserId = userId; plan.Version++;
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = now, Action = "PLAN_RELEASED", TargetObject = $"DispatchPlan:{plan.Id:D}" });
        await db.SaveChangesAsync(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken); return PlanCommandResult.Success(plan.Id);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginAsync(CancellationToken ct) => db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
    private async Task<DispatchPlan?> LockDraftAsync(string floor, CancellationToken ct) => await db.DispatchPlans.FromSqlInterpolated($"SELECT p.* FROM public.\"DispatchPlans\" p JOIN public.\"Zones\" z ON z.\"Id\" = p.\"ZoneId\" WHERE p.\"Status\" = 'DRAFT'::public.dispatch_plan_status AND z.\"Code\" = {floor.Trim()} FOR UPDATE OF p")
        .Include(plan => plan.Trips).ThenInclude(trip => trip.Stops).Include(plan => plan.Trips).ThenInclude(trip => trip.Tasks).Include(plan => plan.Tasks).ThenInclude(task => task.Mission).ThenInclude(mission => mission.MissionType).Include(plan => plan.Tasks).ThenInclude(task => task.Mission).ThenInclude(mission => mission.Item).AsSplitQuery().SingleOrDefaultAsync(ct);
    private static bool Valid(Guid userId, string floor, long version) => userId != Guid.Empty && !string.IsNullOrWhiteSpace(floor) && version > 0;
    private static void SetWaiting(DispatchPlanTask task, string reason) { task.Status = DispatchPlanTaskStatus.Waiting; task.PlanTripId = null; task.PlanTrip = null; task.DeliveryPlanStopId = null; task.DeliveryPlanStop = null; task.SlotNo = null; task.WaitReason = reason; }
    private void RemoveEmptyTrip(DispatchPlan plan, DispatchPlanTrip? trip) { if (trip is null || trip.Tasks.Any(task => task.Status == DispatchPlanTaskStatus.Assigned)) return; db.DispatchPlanStops.RemoveRange(trip.Stops); db.DispatchPlanTrips.Remove(trip); plan.Trips.Remove(trip); }
    private void Touch(DispatchPlan plan, Guid userId, string action) { plan.ManualEditCount++; plan.Version++; db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = clock.GetUtcNow().UtcDateTime, Action = action, TargetObject = $"DispatchPlan:{plan.Id:D}" }); }
    private static string BuildDispatchPayload(Trip trip, Robot robot, DispatchPlanTrip planTrip, IReadOnlyList<DispatchPlanTask> tasks, IReadOnlyDictionary<Guid, string> nodeCodes)
    {
        var payload = new
        {
            orderId = trip.TripCode,
            robotId = robot.RobotCode,
            mapVersionId = trip.MapVersionId,
            stops = planTrip.Stops.OrderBy(stop => stop.Seq).Select(stop => new
            {
                seq = stop.Seq,
                nodeId = nodeCodes[stop.LocationId],
                action = stop.Action.ToString().ToUpperInvariant(),
                taskIds = (stop.Action == TripStopAction.Pickup ? tasks : tasks.Where(task => task.DeliveryPlanStopId == stop.Id)).Select(task => task.Mission.MissionCode).ToList()
            }).ToList(),
            compartments = tasks.OrderBy(task => task.SlotNo).Select(task => new { slot = task.SlotNo, taskId = task.Mission.MissionCode, item = task.Mission.ItemCodeSnapshot ?? task.Mission.Item?.Code, qty = task.Mission.Quantity }).ToList()
        };
        return JsonSerializer.Serialize(payload);
    }
}
