namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Response payload for a single robot type returned by GET /robot-types.
/// Fields reflect the current DB schema (RobotTypes table).
/// </summary>
public sealed record RobotTypeResponse(
    Guid   Id,
    string TypeCode,
    string Name,
    int    Slots,
    string ColorHex,
    string? Description
);
