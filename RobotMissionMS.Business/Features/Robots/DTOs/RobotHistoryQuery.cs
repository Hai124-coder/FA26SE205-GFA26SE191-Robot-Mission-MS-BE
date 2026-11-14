namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Query parameters for GET /robots/{id}/history.
/// Both from and to are optional; when omitted the endpoint returns the last 7 days.
/// </summary>
public sealed class RobotHistoryQuery
{
    /// <summary>Start of the time window (inclusive), ISO 8601.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>End of the time window (inclusive), ISO 8601.</summary>
    public DateTimeOffset? To { get; init; }
}
