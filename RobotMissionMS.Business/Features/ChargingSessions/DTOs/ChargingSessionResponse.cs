namespace RobotMissionMS.Business.Features.ChargingSessions.DTOs;

public sealed class ChargingSessionResponse
{
    public required Guid Id { get; init; }
    public required Guid RobotId { get; init; }
    public required string RobotCode { get; init; }
    public required Guid ChargingStationId { get; init; }
    public required string Station { get; init; }
    public required string Floor { get; init; }
    public required DateTime QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public int? BatteryAtStart { get; init; }
    public int? BatteryAtEnd { get; init; }
    public required string Status { get; init; }
    public required long WaitingDurationSeconds { get; init; }
}
