namespace RobotMissionMS.Business.Features.Trips.Services;

public interface ITripExecutionService
{
    Task<TripExecutionResult> ApplyAsync(TripExecutionEvent input, CancellationToken cancellationToken = default);
}

public enum TripExecutionEventType { Started, Arrived, PickupCompleted, CompartmentEmpty, StopCompleted, Completed }
public enum TripExecutionError { InvalidEvent, TripNotFound, RobotMismatch, StopNotFound, MissionNotFound, InvalidTransition, MissionNotCompleted }

public sealed record TripExecutionEvent(Guid TripId, Guid RobotId, TripExecutionEventType Type, DateTimeOffset OccurredAt, int? StopSequence = null, Guid? MissionId = null);
public sealed class TripExecutionResult
{
    private TripExecutionResult(TripExecutionError? error) => Error = error;
    public TripExecutionError? Error { get; }
    public bool IsSuccess => Error is null;
    public static TripExecutionResult Success() => new(null);
    public static TripExecutionResult Failure(TripExecutionError error) => new(error);
}
