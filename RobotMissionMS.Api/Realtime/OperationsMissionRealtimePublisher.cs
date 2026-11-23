using RobotMissionMS.Business.Features.Tasks.Services;

namespace RobotMissionMS.Api.Realtime;

public sealed class OperationsMissionRealtimePublisher(IOperationsRealtimePublisher publisher) : IMissionRealtimePublisher
{
    public Task PublishTaskUpdatedAsync(string floor, MissionRealtimeUpdate update, CancellationToken cancellationToken = default) => publisher.PublishAsync("task.updated", floor, update, cancellationToken);
    public Task PublishQueueChangedAsync(string floor, int waitingCount, CancellationToken cancellationToken = default) => publisher.PublishAsync("queue.changed", floor, new { waitingCount }, cancellationToken);
}
