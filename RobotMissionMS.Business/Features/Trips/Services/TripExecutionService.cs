using System.Data;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Tasks.Services;
using RobotMissionMS.Business.Features.ActivityLogs.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Trips.Services;

public sealed class TripExecutionService(RobotMissionDbContext db, ITripRealtimePublisher? realtime = null, IMissionRealtimePublisher? missionRealtime = null, IActivityRealtimePublisher? activityRealtime = null) : ITripExecutionService
{
    public async Task<TripExecutionResult> ApplyAsync(TripExecutionEvent input, CancellationToken cancellationToken = default)
    {
        if (input.TripId == Guid.Empty || input.RobotId == Guid.Empty || input.OccurredAt == default) return TripExecutionResult.Failure(TripExecutionError.InvalidEvent);
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        await db.LockRobotAsync(input.RobotId, cancellationToken);
        var trip = await db.Trips.FromSqlInterpolated($"SELECT * FROM public.\"Trips\" WHERE \"Id\" = {input.TripId} FOR UPDATE")
            .Include(value => value.Zone).Include(value => value.Robot).Include(value => value.TripStops).Include(value => value.Missions).SingleOrDefaultAsync(cancellationToken);
        if (trip is null) return TripExecutionResult.Failure(TripExecutionError.TripNotFound);
        if (trip.RobotId != input.RobotId) return TripExecutionResult.Failure(TripExecutionError.RobotMismatch);
        var at = input.OccurredAt.UtcDateTime;
        var previousTripStatus = trip.Status;
        var result = input.Type switch
        {
            TripExecutionEventType.Started => Start(trip, at),
            TripExecutionEventType.Arrived => Arrive(trip, input.StopSequence, at),
            TripExecutionEventType.PickupCompleted => Pickup(trip, input.StopSequence, at),
            TripExecutionEventType.CompartmentEmpty => Empty(trip, input.MissionId, at),
            TripExecutionEventType.StopCompleted => StopCompleted(trip, input.StopSequence, at),
            TripExecutionEventType.Completed => Complete(trip, at),
            _ => TripExecutionResult.Failure(TripExecutionError.InvalidEvent)
        };
        if (!result.IsSuccess) return result;
        if (previousTripStatus != TripStatus.Completed && trip.Status == TripStatus.Completed)
            await db.SetRobotStateAfterWorkAsync(trip.Robot, cancellationToken, finishingTripId: trip.Id);
        var stateChanged = db.ChangeTracker.HasChanges();
        var missionUpdates = db.ChangeTracker.Entries<Mission>()
            .Where(entry => entry.Property(value => value.Status).IsModified)
            .Select(entry => new MissionRealtimeUpdate(entry.Entity.Id, entry.Entity.Status.ToString().ToUpperInvariant(), trip.RobotId, entry.Entity.SlotNo, MissionReason(input.Type)))
            .ToList();
        if (stateChanged)
            AddActivity(input, trip, at);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        if (stateChanged && transaction is not null && realtime is not null)
            await realtime.PublishUpdatedAsync(trip.Zone.Code, ToRealtimeUpdate(trip), cancellationToken);
        if (transaction is not null && missionRealtime is not null)
            foreach (var update in missionUpdates) await missionRealtime.PublishTaskUpdatedAsync(trip.Zone.Code, update, cancellationToken);
        var activity = Activity(input, trip, at);
        if (transaction is not null && activityRealtime is not null && activity is not null)
            await activityRealtime.PublishAsync(trip.Zone.Code, activity, cancellationToken);
        return result;
    }

    private static TripExecutionResult Start(Trip trip, DateTime at)
    {
        if (trip.Status == TripStatus.InProgress) return TripExecutionResult.Success();
        if (trip.Status != TripStatus.Dispatched) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        trip.Status = TripStatus.InProgress; trip.StartedAt ??= at;
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult Arrive(Trip trip, int? sequence, DateTime at)
    {
        var start = Start(trip, at); if (!start.IsSuccess) return start;
        var stop = FindStop(trip, sequence); if (stop is null) return TripExecutionResult.Failure(TripExecutionError.StopNotFound);
        if (trip.TripStops.Any(value => value.Seq < stop.Seq && value.Status != TripStopStatus.Done)) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        if (stop.Status == TripStopStatus.Done) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        stop.Status = TripStopStatus.Arrived; stop.ArrivedAt ??= at;
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult Pickup(Trip trip, int? sequence, DateTime at)
    {
        var arrived = Arrive(trip, sequence, at); if (!arrived.IsSuccess) return arrived;
        var stop = FindStop(trip, sequence)!;
        if (stop.Action != TripStopAction.Pickup) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        foreach (var mission in trip.Missions.Where(value => value.Status == MissionStatus.Assigned))
        {
            var previous = mission.Status; mission.Status = MissionStatus.InProgress; mission.PickedUpAt ??= at; mission.StartedAt ??= at;
            AddHistory(mission, previous, at, "ROBOT_PICKUP_COMPLETED");
        }
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult Empty(Trip trip, Guid? missionId, DateTime at)
    {
        if (missionId is not { } id) return TripExecutionResult.Failure(TripExecutionError.InvalidEvent);
        var mission = trip.Missions.SingleOrDefault(value => value.Id == id); if (mission is null) return TripExecutionResult.Failure(TripExecutionError.MissionNotFound);
        if (mission.Status == MissionStatus.Completed) return TripExecutionResult.Success();
        if (mission.Status != MissionStatus.InProgress) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        if (mission.ToLocationId is null || !trip.TripStops.Any(value => value.LocationId == mission.ToLocationId && value.Action == TripStopAction.Drop && value.Status == TripStopStatus.Arrived))
            return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        var previous = mission.Status; mission.Status = MissionStatus.Completed; mission.CompletedAt ??= at;
        AddHistory(mission, previous, at, "ROBOT_COMPARTMENT_EMPTY");
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult StopCompleted(Trip trip, int? sequence, DateTime at)
    {
        var stop = FindStop(trip, sequence); if (stop is null) return TripExecutionResult.Failure(TripExecutionError.StopNotFound);
        if (stop.Status == TripStopStatus.Done) return TripExecutionResult.Success();
        if (stop.Status != TripStopStatus.Arrived) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        if (stop.Action == TripStopAction.Pickup && trip.Missions.Any(value => value.Status == MissionStatus.Assigned)) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        if (stop.Action == TripStopAction.Drop && trip.Missions.Any(value => value.ToLocationId == stop.LocationId && value.Status != MissionStatus.Completed)) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        stop.Status = TripStopStatus.Done; stop.DepartedAt ??= at;
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult Complete(Trip trip, DateTime at)
    {
        if (trip.Status == TripStatus.Completed) return TripExecutionResult.Success();
        if (trip.Status != TripStatus.InProgress) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        if (trip.Missions.Any(value => value.Status != MissionStatus.Completed)) return TripExecutionResult.Failure(TripExecutionError.MissionNotCompleted);
        if (trip.TripStops.Any(value => value.Status != TripStopStatus.Done)) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        trip.Status = TripStatus.Completed; trip.CompletedAt ??= at;
        return TripExecutionResult.Success();
    }

    private static TripStop? FindStop(Trip trip, int? sequence) => sequence is null ? null : trip.TripStops.SingleOrDefault(value => value.Seq == sequence.Value);
    private static string? MissionReason(TripExecutionEventType type) => type switch
    {
        TripExecutionEventType.PickupCompleted => "ROBOT_PICKUP_COMPLETED",
        TripExecutionEventType.CompartmentEmpty => "ROBOT_COMPARTMENT_EMPTY",
        _ => null
    };
    private static TripRealtimeUpdate ToRealtimeUpdate(Trip trip) => new(
        trip.Id,
        trip.TripStops.OrderBy(value => value.Seq).FirstOrDefault(value => value.Status != TripStopStatus.Done)?.Seq,
        trip.TripStops.OrderBy(value => value.Seq).Select(value => new TripRealtimeStop(value.Seq, value.Action.ToString().ToUpperInvariant(), value.Status.ToString().ToUpperInvariant())).ToList(),
        trip.Missions.OrderBy(value => value.SlotNo).Select(value => new TripRealtimeSlot(value.Id, value.SlotNo, value.Status.ToString().ToUpperInvariant())).ToList());
    private void AddActivity(TripExecutionEvent input, Trip trip, DateTime at)
    {
        var item = Activity(input, trip, at); var entry = item is null ? null : new ActivityLog { Timestamp = item.Timestamp, Action = item.Action, TargetObject = item.TargetObject };
        if (entry is not null) db.ActivityLogs.Add(entry);
    }
    private static ActivityRealtimeEvent? Activity(TripExecutionEvent input, Trip trip, DateTime at) => input.Type switch
    {
        TripExecutionEventType.Started => new(at, "TRIP_STARTED", $"Trip:{trip.Id:D}", null),
        TripExecutionEventType.StopCompleted => new(at, "TRIP_STOP_COMPLETED", $"Trip:{trip.Id:D}", null),
        TripExecutionEventType.CompartmentEmpty when input.MissionId is { } missionId => new(at, "MISSION_COMPLETED", $"Mission:{missionId:D}", null),
        TripExecutionEventType.Completed => new(at, "TRIP_COMPLETED", $"Trip:{trip.Id:D}", null), _ => null
    };
    private static void AddHistory(Mission mission, MissionStatus from, DateTime at, string reason) => mission.MissionStatusHistories.Add(new MissionStatusHistory { Id = Guid.NewGuid(), MissionId = mission.Id, FromStatus = from, ToStatus = mission.Status, ChangedAt = at, Reason = reason });
}
