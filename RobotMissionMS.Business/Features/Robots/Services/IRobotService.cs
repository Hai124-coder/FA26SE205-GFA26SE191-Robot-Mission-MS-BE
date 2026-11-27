using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public interface IRobotService
{
    /// <summary>
    /// Returns robots matching the query filters.
    /// An assigned operator floor takes precedence over the query filter.
    /// </summary>
    Task<RobotListResult> GetRobotsAsync(
        RobotListQuery    query,
        string?           operatorFloor,
        CancellationToken cancellationToken = default);

    Task<string?> FindRobotFloorAsync(Guid robotId, CancellationToken cancellationToken = default);

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
        Guid actorUserId, CreateRobotRequest request,
        CancellationToken  cancellationToken = default);

    /// <summary>
    /// Updates HomeChargingStationId of a robot. Admin only.
    /// </summary>
    Task<UpdateRobotResult> UpdateRobotAsync(
        Guid actorUserId, Guid robotId,
        UpdateRobotRequest request,
        CancellationToken  cancellationToken = default);

    /// <summary>
    /// Soft-deletes a robot (sets State = INACTIVE). Refused if robot is on an active trip.
    /// </summary>
    Task<DeleteRobotResult> DeleteRobotAsync(
        Guid actorUserId, Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a robot so it finishes its current trip then stops accepting new ones.
    /// </summary>
    Task<SuspendRobotResult> SuspendRobotAsync(
        Guid actorUserId, Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a suspended robot so it can accept new trips again.
    /// </summary>
    Task<ResumeRobotResult> ResumeRobotAsync(
        Guid actorUserId, Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an idle (AVAILABLE) robot to its charging station.
    /// </summary>
    Task<ChargeRobotResult> ChargeRobotAsync(
        Guid actorUserId, Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a robot into maintenance mode. Robot must not be on an active trip.
    /// </summary>
    Task<MaintenanceRobotResult> MaintenanceRobotAsync(
        Guid actorUserId, Guid robotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a robot in MAINTENANCE or ERROR to AVAILABLE.
    /// </summary>
    Task<BackToServiceRobotResult> BackToServiceAsync(
        Guid actorUserId, Guid robotId,
        CancellationToken cancellationToken = default);
}
