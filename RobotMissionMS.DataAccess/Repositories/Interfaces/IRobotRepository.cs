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
