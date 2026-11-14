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

    /// <summary>
    /// Creates a new robot. Returns the created row or an error code.
    /// </summary>
    Task<CreateRobotRepositoryResult> CreateRobotAsync(
        string  robotCode,
        Guid    robotTypeId,
        Guid    zoneId,
        Guid?   homeChargingStationId,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up a RobotType id by TypeCode. Returns null if not found.</summary>
    Task<Guid?> FindRobotTypeIdByCodeAsync(string typeCode, CancellationToken cancellationToken = default);

    /// <summary>Looks up a Zone id by Code. Returns null if not found.</summary>
    Task<Guid?> FindZoneIdByCodeAsync(string zoneCode, CancellationToken cancellationToken = default);

    /// <summary>Returns true when a ChargingStation with the given id exists.</summary>
    Task<bool> ChargingStationExistsAsync(Guid stationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates HomeChargingStationId of a robot. Returns the updated row or null if not found.
    /// </summary>
    Task<CreateRobotRow?> UpdateRobotAsync(
        Guid  robotId,
        Guid? homeChargingStationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a robot by setting State = INACTIVE.
    /// Returns false when robot not found, throws when robot has an active trip.
    /// </summary>
    Task<DeleteRobotRepositoryResult> DeleteRobotAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a robot: sets State = MAINTENANCE so it finishes current trip
    /// then stops accepting new ones. (SUSPENDED not in current DB schema.)
    /// </summary>
    Task<SuspendRobotRepositoryResult> SuspendRobotAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a suspended robot: sets State back to AVAILABLE.
    /// Only valid when State = MAINTENANCE (suspended).
    /// </summary>
    Task<ResumeRobotRepositoryResult> ResumeRobotAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an idle robot to charge: sets State = CHARGING.
    /// Only valid when State = AVAILABLE.
    /// </summary>
    Task<ChargeRobotRepositoryResult> ChargeRobotAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);
}

public enum DeleteRobotRepositoryError { NotFound, OnActiveTrip }

public sealed class DeleteRobotRepositoryResult
{
    public bool IsSuccess { get; private init; }
    public DeleteRobotRepositoryError? Error { get; private init; }

    public static DeleteRobotRepositoryResult Success() => new() { IsSuccess = true };
    public static DeleteRobotRepositoryResult Failure(DeleteRobotRepositoryError e) =>
        new() { IsSuccess = false, Error = e };
}

public enum CreateRobotRepositoryError
{
    DuplicateCode,
}

public sealed class CreateRobotRepositoryResult
{
    public bool IsSuccess { get; private init; }
    public CreateRobotRow? Row { get; private init; }
    public CreateRobotRepositoryError? Error { get; private init; }

    public static CreateRobotRepositoryResult Success(CreateRobotRow row) =>
        new() { IsSuccess = true, Row = row };
    public static CreateRobotRepositoryResult Failure(CreateRobotRepositoryError error) =>
        new() { IsSuccess = false, Error = error };
}

public sealed record CreateRobotRow(
    Guid      Id,
    string    RobotCode,
    string    TypeCode,
    string    TypeName,
    string    FloorCode,
    string    FloorName,
    string    State,
    bool      IsConnected,
    int       Battery,
    string?   HomeStationCode,
    DateTime  CreatedAt
);

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

public enum SuspendRobotRepositoryError { NotFound, AlreadySuspended }

public sealed class SuspendRobotRepositoryResult
{
    public bool IsSuccess { get; private init; }
    public SuspendRobotRepositoryError? Error { get; private init; }
    public static SuspendRobotRepositoryResult Success() => new() { IsSuccess = true };
    public static SuspendRobotRepositoryResult Failure(SuspendRobotRepositoryError e) =>
        new() { IsSuccess = false, Error = e };
}

public enum ResumeRobotRepositoryError { NotFound, NotSuspended }

public sealed class ResumeRobotRepositoryResult
{
    public bool IsSuccess { get; private init; }
    public ResumeRobotRepositoryError? Error { get; private init; }
    public static ResumeRobotRepositoryResult Success() => new() { IsSuccess = true };
    public static ResumeRobotRepositoryResult Failure(ResumeRobotRepositoryError e) =>
        new() { IsSuccess = false, Error = e };
}

public enum ChargeRobotRepositoryError { NotFound, NotAvailable }

public sealed class ChargeRobotRepositoryResult
{
    public bool IsSuccess { get; private init; }
    public ChargeRobotRepositoryError? Error { get; private init; }
    public static ChargeRobotRepositoryResult Success() => new() { IsSuccess = true };
    public static ChargeRobotRepositoryResult Failure(ChargeRobotRepositoryError e) =>
        new() { IsSuccess = false, Error = e };
}
