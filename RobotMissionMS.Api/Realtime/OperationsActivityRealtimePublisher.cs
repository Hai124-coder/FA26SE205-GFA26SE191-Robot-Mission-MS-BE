using RobotMissionMS.Business.Features.ActivityLogs.Services;

namespace RobotMissionMS.Api.Realtime;

public sealed class OperationsActivityRealtimePublisher(IOperationsRealtimePublisher publisher) : IActivityRealtimePublisher
{
    public Task PublishAsync(string? floor, ActivityRealtimeEvent item, CancellationToken cancellationToken = default) =>
        publisher.PublishAsync("event.logged", floor, item, cancellationToken);
}
