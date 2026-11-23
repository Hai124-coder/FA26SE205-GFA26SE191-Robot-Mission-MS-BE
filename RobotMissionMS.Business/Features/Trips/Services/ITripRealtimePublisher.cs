namespace RobotMissionMS.Business.Features.Trips.Services;

/// <summary>Publishes a committed trip state change to realtime clients.</summary>
public interface ITripRealtimePublisher
{
    Task PublishUpdatedAsync(string floor, TripRealtimeUpdate update, CancellationToken cancellationToken = default);
}

public sealed record TripRealtimeUpdate(
    Guid TripId,
    int? Idx,
    IReadOnlyList<TripRealtimeStop> Stops,
    IReadOnlyList<TripRealtimeSlot> Slots);

public sealed record TripRealtimeStop(int Seq, string Action, string Status);
public sealed record TripRealtimeSlot(Guid MissionId, int? Slot, string Status);
