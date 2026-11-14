using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public interface IRobotService
{
    /// <summary>
    /// Returns robots matching the query filters.
    /// Floor restriction per Operator is deferred until the User schema
    /// gains a zone assignment column.
    /// </summary>
    Task<RobotListResult> GetRobotsAsync(
        RobotListQuery    query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full detail for one robot, or NotFound.
    /// </summary>
    Task<RobotDetailResult> GetRobotDetailAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns trips, alerts and charging sessions for one robot within the
    /// requested time window. Returns NotFound when the robot does not exist.
    /// </summary>
    Task<RobotHistoryResult> GetRobotHistoryAsync(
        Guid                robotId,
        RobotHistoryQuery   query,
        CancellationToken   cancellationToken = default);
}
