namespace RobotMissionMS.Business.Features.Tasks.Services;

public interface IMissionRealtimePublisher
{
    Task PublishTaskUpdatedAsync(string floor, MissionRealtimeUpdate update, CancellationToken cancellationToken = default);
    Task PublishQueueChangedAsync(string floor, int waitingCount, CancellationToken cancellationToken = default);
}

public sealed record MissionRealtimeUpdate(Guid Id, string Status, Guid? RobotId, int? Slot, string? Reason);
