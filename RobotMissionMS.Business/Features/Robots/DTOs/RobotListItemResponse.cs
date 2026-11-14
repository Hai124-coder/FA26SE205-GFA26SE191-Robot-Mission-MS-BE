namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// One robot in the GET /robots list.
/// Position and battery change frequently; frontend takes this as the initial
/// snapshot then receives live updates via SignalR robot.updated events.
/// </summary>
public sealed class RobotListItemResponse
{
    public required Guid   Id          { get; init; }
    public required string RobotCode   { get; init; }

    /// <summary>TypeCode of the robot type, e.g. "MED_CART".</summary>
    public required string Type        { get; init; }

    /// <summary>TypeCode of the robot type human-readable name.</summary>
    public required string TypeName    { get; init; }

    /// <summary>Zone/floor code the robot belongs to, e.g. "F2".</summary>
    public required string Floor       { get; init; }

    /// <summary>Zone/floor human-readable name.</summary>
    public required string FloorName   { get; init; }

    /// <summary>
    /// Current robot state: INACTIVE | AVAILABLE | ON_TRIP | CHARGING | ERROR | MAINTENANCE.
    /// </summary>
    public required string State       { get; init; }

    public required bool   IsConnected { get; init; }

    /// <summary>Battery percentage 0–100.</summary>
    public required int    Battery     { get; init; }

    public double? CurrentX            { get; init; }
    public double? CurrentY            { get; init; }

    /// <summary>TripCode of the active trip, null when no trip is running.</summary>
    public string? ActiveTripCode      { get; init; }

    public DateTime? LastHeartbeatAt   { get; init; }
}
