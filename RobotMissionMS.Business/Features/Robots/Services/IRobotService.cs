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

    /// <summary>
    /// Creates a new robot with State = INACTIVE and IsConnected = false.
    /// </summary>
    Task<CreateRobotResult> CreateRobotAsync(
        CreateRobotRequest request,
        CancellationToken  cancellationToken = default);

    /// <summary>
    /// Updates HomeChargingStationId of a robot. Admin only.
    /// </summary>
    Task<UpdateRobotResult> UpdateRobotAsync(
        Guid              robotId,
        UpdateRobotRequest request,
        CancellationToken  cancellationToken = default);

    /// <summary>
    /// Soft-deletes a robot (sets State = INACTIVE). Refused if robot is on an active trip.
    /// </summary>
    Task<DeleteRobotResult> DeleteRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a robot so it finishes its current trip then stops accepting new ones.
    /// </summary>
    Task<SuspendRobotResult> SuspendRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a suspended robot so it can accept new trips again.
    /// </summary>
    Task<ResumeRobotResult> ResumeRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an idle (AVAILABLE) robot to its charging station.
    /// </summary>
    Task<ChargeRobotResult> ChargeRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a robot into maintenance mode. Robot must not be on an active trip.
    /// </summary>
    Task<MaintenanceRobotResult> MaintenanceRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default);
}
