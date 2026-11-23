using RobotMissionMS.Business.Features.Trips.Services;

namespace RobotMissionMS.Api.Realtime;

public sealed class OperationsTripRealtimePublisher(IOperationsRealtimePublisher publisher) : ITripRealtimePublisher
{
    public Task PublishUpdatedAsync(string floor, TripRealtimeUpdate update, CancellationToken cancellationToken = default) =>
        publisher.PublishAsync("trip.updated", floor, update, cancellationToken);
}
