namespace RobotMissionMS.Api.Realtime;

public interface IRobotStateRealtimePublisher
{
    Task PublishUpdatedAsync(string floor, RobotRealtimeUpdate update, CancellationToken cancellationToken = default);
}

public sealed class OperationsRobotRealtimePublisher(IOperationsRealtimePublisher publisher) : IRobotStateRealtimePublisher
{
    public Task PublishUpdatedAsync(string floor, RobotRealtimeUpdate update, CancellationToken cancellationToken = default) =>
        publisher.PublishAsync("robot.updated", floor, update, cancellationToken);
}

/// <summary>Live robot state sent to clients viewing the robot's floor.</summary>
public sealed record RobotRealtimeUpdate(
    Guid Id,
    string Floor,
    double? X,
    double? Y,
    int Battery,
    string Status,
    bool IsConnected,
    Guid? TripId,
    string? NextNodeId,
    int? AtStopSeq,
    DateTime ReceivedAt);
