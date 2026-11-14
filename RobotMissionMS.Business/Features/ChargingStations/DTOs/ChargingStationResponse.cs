namespace RobotMissionMS.Business.Features.ChargingStations.DTOs;

/// <summary>
/// Current station snapshot. The present schema records active charging sessions,
/// but does not contain a charging-wait queue.
/// </summary>
public sealed class ChargingStationResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Floor { get; init; }
    public required string FloorName { get; init; }
    public required string StationType { get; init; }
    public required string Status { get; init; }
    public required int Capacity { get; init; }
    public required int UsedPorts { get; init; }
    public required int FreePorts { get; init; }
    public required IReadOnlyList<ChargingRobotResponse> ChargingRobots { get; init; }
}

public sealed class ChargingRobotResponse
{
    public required Guid Id { get; init; }
    public required string RobotCode { get; init; }
    public required int Battery { get; init; }
    public required DateTime StartedAt { get; init; }
}
