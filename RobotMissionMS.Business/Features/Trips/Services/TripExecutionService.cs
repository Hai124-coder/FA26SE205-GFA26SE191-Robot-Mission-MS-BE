using System.Data;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Trips.Services;

public sealed class TripExecutionService(RobotMissionDbContext db) : ITripExecutionService
{
    public async Task<TripExecutionResult> ApplyAsync(TripExecutionEvent input, CancellationToken cancellationToken = default)
    {
        if (input.TripId == Guid.Empty || input.RobotId == Guid.Empty || input.OccurredAt == default) return TripExecutionResult.Failure(TripExecutionError.InvalidEvent);
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var trip = await db.Trips.FromSqlInterpolated($"SELECT * FROM public.\"Trips\" WHERE \"Id\" = {input.TripId} FOR UPDATE")
            .Include(value => value.Robot).Include(value => value.TripStops).Include(value => value.Missions).SingleOrDefaultAsync(cancellationToken);
        if (trip is null) return TripExecutionResult.Failure(TripExecutionError.TripNotFound);
        if (trip.RobotId != input.RobotId) return TripExecutionResult.Failure(TripExecutionError.RobotMismatch);
        var at = input.OccurredAt.UtcDateTime;
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
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
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
        var previous = mission.Status; mission.Status = MissionStatus.Completed; mission.CompletedAt ??= at;
        AddHistory(mission, previous, at, "ROBOT_COMPARTMENT_EMPTY");
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult StopCompleted(Trip trip, int? sequence, DateTime at)
    {
        var stop = FindStop(trip, sequence); if (stop is null) return TripExecutionResult.Failure(TripExecutionError.StopNotFound);
        if (stop.Status == TripStopStatus.Done) return TripExecutionResult.Success();
        if (stop.Status != TripStopStatus.Arrived) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        stop.Status = TripStopStatus.Done; stop.DepartedAt ??= at;
        return TripExecutionResult.Success();
    }

    private static TripExecutionResult Complete(Trip trip, DateTime at)
    {
        if (trip.Status == TripStatus.Completed) return TripExecutionResult.Success();
        if (trip.Status != TripStatus.InProgress) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        if (trip.Missions.Any(value => value.Status != MissionStatus.Completed)) return TripExecutionResult.Failure(TripExecutionError.MissionNotCompleted);
        if (trip.TripStops.Any(value => value.Status != TripStopStatus.Done)) return TripExecutionResult.Failure(TripExecutionError.InvalidTransition);
        trip.Status = TripStatus.Completed; trip.CompletedAt ??= at; trip.Robot.State = RobotState.Available;
        return TripExecutionResult.Success();
    }

    private static TripStop? FindStop(Trip trip, int? sequence) => sequence is null ? null : trip.TripStops.SingleOrDefault(value => value.Seq == sequence.Value);
    private static void AddHistory(Mission mission, MissionStatus from, DateTime at, string reason) => mission.MissionStatusHistories.Add(new MissionStatusHistory { Id = Guid.NewGuid(), MissionId = mission.Id, FromStatus = from, ToStatus = mission.Status, ChangedAt = at, Reason = reason });
}
