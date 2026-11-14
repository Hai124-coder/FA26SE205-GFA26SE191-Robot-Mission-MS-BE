namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Full detail for GET /robots/{id} (SC-14).
/// </summary>
public sealed class RobotDetailResponse
{
    public required Guid    Id             { get; init; }
    public required string  RobotCode      { get; init; }
    public required string  Type           { get; init; }
    public required string  TypeName       { get; init; }
    public required string  Floor          { get; init; }
    public required string  FloorName      { get; init; }
    public required string  State          { get; init; }
    public required bool    IsConnected    { get; init; }
    public required int     Battery        { get; init; }
    public double?          CurrentX       { get; init; }
    public double?          CurrentY       { get; init; }
    public DateTime?        LastHeartbeatAt { get; init; }

    /// <summary>Home charging station code, null for schedule-only robots with no assigned station.</summary>
    public string?          HomeStation    { get; init; }

    /// <summary>One entry per physical slot on this robot type. Empty for WASTE_CART / CLEAN_BOT.</summary>
    public required IReadOnlyList<RobotSlotInfo> Slots { get; init; }

    /// <summary>Active trip (DISPATCHED or IN_PROGRESS), null when idle.</summary>
    public RobotActiveTripInfo? ActiveTrip { get; init; }

    /// <summary>Alerts with status OPEN linked to this robot.</summary>
    public required IReadOnlyList<RobotAlertInfo> OpenAlerts { get; init; }
}

/// <summary>State of one cargo slot on the robot.</summary>
public sealed class RobotSlotInfo
{
    public required int     SlotNo        { get; init; }

    /// <summary>True when a mission is currently assigned to this slot.</summary>
    public required bool    IsOccupied    { get; init; }

    /// <summary>Mission code occupying this slot, null when empty.</summary>
    public string?          MissionCode   { get; init; }

    /// <summary>Item code being carried, null when empty.</summary>
    public string?          ItemCode      { get; init; }

    /// <summary>Item name, null when empty.</summary>
    public string?          ItemName      { get; init; }

    /// <summary>Quantity in this slot, null when empty.</summary>
    public int?             Quantity      { get; init; }

    /// <summary>Destination department/location code, null when empty.</summary>
    public string?          ToLocationCode { get; init; }

    /// <summary>Destination name, null when empty.</summary>
    public string?          ToLocationName { get; init; }

    /// <summary>
    /// Current mission status for this slot, null when empty.
    /// Values: PENDING | ASSIGNED | IN_PROGRESS | COMPLETED | DELIVERY_FAILED | etc.
    /// </summary>
    public string?          MissionStatus { get; init; }
}

/// <summary>Summary of the robot's active trip.</summary>
public sealed class RobotActiveTripInfo
{
    public required Guid    TripId        { get; init; }
    public required string  TripCode      { get; init; }
    public required string  Status        { get; init; }
    public DateTime?        StartedAt     { get; init; }

    /// <summary>Ordered stops; ArrivedAt/DepartedAt null for future stops.</summary>
    public required IReadOnlyList<RobotTripStopInfo> Stops { get; init; }
}

public sealed class RobotTripStopInfo
{
    public required int     Seq           { get; init; }
    public required string  LocationCode  { get; init; }
    public required string  LocationName  { get; init; }
    public DateTime?        ArrivedAt     { get; init; }
    public DateTime?        DepartedAt    { get; init; }
}

/// <summary>Open alert linked to this robot.</summary>
public sealed class RobotAlertInfo
{
    public required Guid    AlertId       { get; init; }
    public required string  AlertType     { get; init; }
    public required string  Severity      { get; init; }
    public required string  Category      { get; init; }
    public required string  Message       { get; init; }
    public required DateTime CreatedAt    { get; init; }
}
