namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>Accepted command, queued durably for MQTT delivery.</summary>
public sealed class RobotControlCommandResponse
{
    public Guid CommandId { get; init; }
    public Guid RobotId { get; init; }
    public string Command { get; init; } = string.Empty;
    public string Status { get; init; } = "PENDING";
    public DateTime IssuedAt { get; init; }
}
