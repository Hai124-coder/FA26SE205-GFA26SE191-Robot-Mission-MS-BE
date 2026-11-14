namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Response for GET /robots/{id}/history.
/// Contains trips, alerts and charging sessions within the requested time window.
/// </summary>
public sealed class RobotHistoryResponse
{
    public required IReadOnlyList<RobotHistoryTripItem>           Trips            { get; init; }
    public required IReadOnlyList<RobotHistoryAlertItem>          Alerts           { get; init; }
    public required IReadOnlyList<RobotHistoryChargingSessionItem> ChargingSessions { get; init; }
}

public sealed class RobotHistoryTripItem
{
    public required Guid      TripId      { get; init; }
    public required string    TripCode    { get; init; }

    /// <summary>PLANNED | DISPATCHED | IN_PROGRESS | COMPLETED | ABORTED</summary>
    public required string    Status      { get; init; }

    public required int       MissionCount { get; init; }
    public DateTime?          StartedAt    { get; init; }
    public DateTime?          CompletedAt  { get; init; }
    public required DateTime  CreatedAt    { get; init; }
}

public sealed class RobotHistoryAlertItem
{
    public required Guid     AlertId    { get; init; }
    public required string   AlertType  { get; init; }
    public required string   Severity   { get; init; }
    public required string   Category   { get; init; }
    public required string   Status     { get; init; }
    public required string   Message    { get; init; }
    public required DateTime CreatedAt  { get; init; }
    public DateTime?         ResolvedAt { get; init; }
}

public sealed class RobotHistoryChargingSessionItem
{
    public required Guid     SessionId          { get; init; }
    public required string   StationName        { get; init; }
    public required string   StationLocationCode { get; init; }
    public required DateTime StartedAt          { get; init; }
    public DateTime?         EndedAt            { get; init; }

    /// <summary>
    /// Duration in minutes, null when session is still active.
    /// </summary>
    public double?           DurationMinutes    { get; init; }
}
