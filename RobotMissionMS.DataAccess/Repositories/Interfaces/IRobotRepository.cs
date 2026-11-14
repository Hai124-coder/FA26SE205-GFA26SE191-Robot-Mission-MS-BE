using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IRobotRepository
{
    /// <summary>
    /// Returns all robots matching the given filters, ordered by RobotCode.
    /// Pass floorCode to restrict to one zone (Operator sees only their floor).
    /// </summary>
    Task<IReadOnlyList<RobotListRow>> GetRobotsAsync(
        string?     floorCode,
        string?     typeCode,
        RobotState? state,
        bool?       isConnected,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full detail for one robot including active trip stops and open alerts.
    /// Returns null when no robot with the given id exists.
    /// </summary>
    Task<RobotDetailRow?> GetRobotByIdAsync(Guid robotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns trips, alerts and charging sessions for one robot within [fromUtc, toUtc].
    /// Returns null when no robot with the given id exists.
    /// </summary>
    Task<RobotHistoryData?> GetRobotHistoryAsync(
        Guid     robotId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RobotListRow(
    Guid      Id,
    string    RobotCode,
    string    TypeCode,
    string    TypeName,
    string    FloorCode,
    string    FloorName,
    RobotState State,
    bool      IsConnected,
    int       Battery,
    double?   CurrentX,
    double?   CurrentY,
    string?   ActiveTripCode,
    DateTime? LastHeartbeatAt
);

public sealed record RobotDetailRow(
    Guid       Id,
    string     RobotCode,
    string     TypeCode,
    string     TypeName,
    int        Slots,
    string     FloorCode,
    string     FloorName,
    RobotState State,
    bool       IsConnected,
    int        Battery,
    double?    CurrentX,
    double?    CurrentY,
    DateTime?  LastHeartbeatAt,
    string?    HomeStationCode,
    IReadOnlyList<RobotMissionSlotRow>  MissionSlots,
    RobotActiveTripRow?                 ActiveTrip,
    IReadOnlyList<RobotAlertRow>        OpenAlerts
);

public sealed record RobotMissionSlotRow(
    int      SlotNo,
    string   MissionCode,
    string?  ItemCode,
    string?  ItemName,
    int?     Quantity,
    string?  ToLocationCode,
    string?  ToLocationName,
    string   MissionStatus
);

public sealed record RobotActiveTripRow(
    Guid     TripId,
    string   TripCode,
    TripStatus Status,
    DateTime?  StartedAt,
    IReadOnlyList<RobotTripStopRow> Stops
);

public sealed record RobotTripStopRow(
    int      Seq,
    string   LocationCode,
    string   LocationName,
    DateTime? ArrivedAt,
    DateTime? DepartedAt
);

public sealed record RobotAlertRow(
    Guid          AlertId,
    string        AlertType,
    AlertSeverity Severity,
    AlertCategory Category,
    string        Message,
    DateTime      CreatedAt
);

public sealed record RobotHistoryData(
    IReadOnlyList<RobotHistoryTripRow>            Trips,
    IReadOnlyList<RobotHistoryAlertRow>           Alerts,
    IReadOnlyList<RobotHistoryChargingSessionRow> ChargingSessions
);

public sealed record RobotHistoryTripRow(
    Guid       TripId,
    string     TripCode,
    TripStatus Status,
    int        MissionCount,
    DateTime?  StartedAt,
    DateTime?  CompletedAt,
    DateTime   CreatedAt
);

public sealed record RobotHistoryAlertRow(
    Guid          AlertId,
    string        AlertType,
    AlertSeverity Severity,
    AlertCategory Category,
    AlertStatus   Status,
    string        Message,
    DateTime      CreatedAt,
    DateTime?     ResolvedAt
);

public sealed record RobotHistoryChargingSessionRow(
    Guid     SessionId,
    string   StationName,
    string   StationLocationCode,
    DateTime StartedAt,
    DateTime? EndedAt
);
