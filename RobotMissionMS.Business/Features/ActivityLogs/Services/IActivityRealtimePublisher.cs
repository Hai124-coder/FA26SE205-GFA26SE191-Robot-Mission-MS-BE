namespace RobotMissionMS.Business.Features.ActivityLogs.Services;

public interface IActivityRealtimePublisher
{
    Task PublishAsync(string? floor, ActivityRealtimeEvent item, CancellationToken cancellationToken = default);
}

public sealed record ActivityRealtimeEvent(DateTime Timestamp, string Action, string TargetObject, Guid? ActorUserId);
