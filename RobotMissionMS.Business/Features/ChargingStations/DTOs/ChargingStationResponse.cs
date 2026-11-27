namespace RobotMissionMS.Business.Features.ChargingStations.DTOs;

public sealed class ChargingStationListQuery
{
    [System.ComponentModel.DataAnnotations.StringLength(50)] public string? Floor { get; init; }
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [System.ComponentModel.DataAnnotations.Range(1, 100)] public int PageSize { get; init; } = 20;
}

/// <summary>
/// Current station snapshot, including robots occupying ports and robots waiting for one.
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
    public required IReadOnlyList<QueuedRobotResponse> QueuedRobots { get; init; }
}

public sealed class ChargingRobotResponse
{
    public required Guid Id { get; init; }
    public required string RobotCode { get; init; }
    public required int Battery { get; init; }
    public required DateTime StartedAt { get; init; }
}

public sealed class QueuedRobotResponse
{
    public required Guid Id { get; init; }
    public required string RobotCode { get; init; }
    public required int Battery { get; init; }
    public required DateTime QueuedAt { get; init; }
}
