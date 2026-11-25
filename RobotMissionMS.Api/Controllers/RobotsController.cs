using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.Business.Features.Robots.Services;
using RobotMissionMS.Business.Features.ChargingSessions.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/robots")]
[Authorize]
public sealed class RobotsController(
    IRobotService robotService,
    IChargingSessionService chargingSessions,
    IRobotControlCommandService controlCommands) : ControllerBase
{
    /// <summary>
    /// Returns robots with their current state snapshot (SC-05, SC-13, SC-14).
    /// Supports optional filters: floor, type, state, isConnected.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RobotListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RobotListItemResponse>>> GetRobots(
        [FromQuery] RobotListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await robotService.GetRobotsAsync(query, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            RobotListError.InvalidQuery => Problem(
                type: "ROBOTS_INVALID_QUERY",
                title: "The robot list query is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException("Robot service returned an invalid list result.")
        };
    }

    /// <summary>
    /// Returns full detail for one robot: state, slot contents, active trip, open alerts (SC-14).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RobotDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotDetailResponse>> GetRobotById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await robotService.GetRobotDetailAsync(id, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            RobotDetailError.NotFound => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            _ => throw new InvalidOperationException("Robot service returned an invalid detail result.")
        };
    }

    /// <summary>
    /// Queues an immediate STOP or RESUME command for a connected robot.
    /// STOP is a safety stop; RESUME only clears that stop on the robot and does not restart a prior trip.
    /// </summary>
    [HttpPost("{id:guid}/commands")]
    [Authorize(Policy = AuthPolicies.OperatorOrAdmin)]
    [ProducesResponseType(typeof(RobotControlCommandResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RobotControlCommandResponse>> QueueControlCommand(
        Guid id,
        [FromBody] RobotControlCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();

        var operatorFloor = User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
        if (User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(operatorFloor))
        {
            return Problem(
                type: "OPERATOR_FLOOR_NOT_CONFIGURED",
                title: "The operator account has no assigned floor.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var result = await controlCommands.EnqueueAsync(actorUserId, id, operatorFloor, request, cancellationToken);
        if (result.IsSuccess)
            return AcceptedAtAction(nameof(GetRobotById), new { id }, result.Response);

        return result.Error switch
        {
            RobotControlCommandError.InvalidRequest => Problem(
                type: "ROBOT_COMMAND_INVALID_REQUEST",
                title: "Command must be STOP or RESUME.",
                statusCode: StatusCodes.Status400BadRequest),
            RobotControlCommandError.RobotNotFound => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            RobotControlCommandError.FloorForbidden => Problem(
                type: "ROBOT_COMMAND_FLOOR_FORBIDDEN",
                title: "The robot is outside the operator's assigned floor.",
                statusCode: StatusCodes.Status403Forbidden),
            RobotControlCommandError.RobotNotConnected => Problem(
                type: "ROBOT_COMMAND_ROBOT_OFFLINE",
                title: "The robot is not connected, so the command was not queued.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot control command service returned an invalid result.")
        };
    }

    /// <summary>
    /// Returns trips, alerts and charging sessions for one robot within a time window (SC-14).
    /// Query params: from, to (ISO 8601). Defaults to the last 7 days when omitted.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(RobotHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotHistoryResponse>> GetRobotHistory(
        Guid id,
        [FromQuery] RobotHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await robotService.GetRobotHistoryAsync(id, query, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            RobotHistoryError.NotFound => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            _ => throw new InvalidOperationException("Robot service returned an invalid history result.")
        };
    }

    /// <summary>
    /// Creates a new robot (SC-05). Admin only.
    /// Robot is created with State = INACTIVE and IsConnected = false.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(CreateRobotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateRobotResponse>> CreateRobot(
        [FromBody] CreateRobotRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.CreateRobotAsync(actorUserId, request, cancellationToken);
        if (result.IsSuccess)
            return CreatedAtAction(
                nameof(GetRobotById),
                new { id = result.Response!.Id },
                result.Response);

        return result.Error switch
        {
            CreateRobotError.InvalidRequest      => Problem(
                type: "ROBOTS_INVALID_REQUEST",
                title: "The create robot request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            CreateRobotError.RobotTypeNotFound   => Problem(
                type: "ROBOTS_TYPE_NOT_FOUND",
                title: "The specified robot type was not found.",
                statusCode: StatusCodes.Status400BadRequest),
            CreateRobotError.ZoneNotFound        => Problem(
                type: "ROBOTS_ZONE_NOT_FOUND",
                title: "The specified zone/floor was not found or is inactive.",
                statusCode: StatusCodes.Status400BadRequest),
            CreateRobotError.HomeStationNotFound => Problem(
                type: "ROBOTS_STATION_NOT_FOUND",
                title: "The specified home charging station was not found.",
                statusCode: StatusCodes.Status400BadRequest),
            CreateRobotError.DuplicateCode       => Problem(
                type: "ROBOTS_DUPLICATE_CODE",
                title: "A robot with this code already exists.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot service returned an invalid create result.")
        };
    }

    /// <summary>
    /// Updates a robot's home charging station (SC-05). Admin only.
    /// Notes column is not in the current schema.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(CreateRobotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateRobotResponse>> UpdateRobot(
        Guid id,
        [FromBody] UpdateRobotRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.UpdateRobotAsync(actorUserId, id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            UpdateRobotError.NotFound           => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            UpdateRobotError.HomeStationNotFound => Problem(
                type: "ROBOTS_STATION_NOT_FOUND",
                title: "The specified home charging station was not found.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException("Robot service returned an invalid update result.")
        };
    }

    /// <summary>
    /// Soft-deletes a robot by setting State = INACTIVE (SC-05). Admin only.
    /// Refused with 409 if the robot is currently on an active trip.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRobot(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.DeleteRobotAsync(actorUserId, id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return result.Error switch
        {
            DeleteRobotError.NotFound        => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            DeleteRobotError.RobotOnActiveTrip => Problem(
                type: "ROBOTS_ON_ACTIVE_TRIP",
                title: "Cannot decommission a robot that is currently on an active trip.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot service returned an invalid delete result.")
        };
    }

    /// <summary>
    /// Suspends a robot: it finishes its current trip then stops accepting new ones (SC-14).
    /// Operator or Admin. State is mapped to MAINTENANCE (SUSPENDED not in current DB schema).
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = AuthPolicies.OperatorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SuspendRobot(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.SuspendRobotAsync(actorUserId, id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return result.Error switch
        {
            SuspendRobotError.NotFound         => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            SuspendRobotError.AlreadySuspended => Problem(
                type: "ROBOTS_ALREADY_SUSPENDED",
                title: "The robot is already suspended or decommissioned.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot service returned an invalid suspend result.")
        };
    }

    /// <summary>
    /// Resumes a suspended robot so it can accept new trips again (SC-14).
    /// Operator or Admin.
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = AuthPolicies.OperatorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResumeRobot(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.ResumeRobotAsync(actorUserId, id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return result.Error switch
        {
            ResumeRobotError.NotFound     => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            ResumeRobotError.NotSuspended => Problem(
                type: "ROBOTS_NOT_SUSPENDED",
                title: "The robot is not currently suspended.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot service returned an invalid resume result.")
        };
    }

    /// <summary>
    /// Queues an idle robot for its configured home charging station (SC-14).
    /// </summary>
    [HttpPost("{id:guid}/charge")]
    [Authorize(Policy = AuthPolicies.OperatorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChargeRobot(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await chargingSessions.QueueToHomeStationAsync(id, actorUserId, DateTime.UtcNow, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return result.Error switch
        {
            ChargingSessionCommandError.RobotNotFound => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            ChargingSessionCommandError.RobotHasNoHomeStation => Problem(
                type: "ROBOT_HOME_CHARGING_STATION_NOT_CONFIGURED",
                title: "The robot has no configured home charging station.",
                statusCode: StatusCodes.Status409Conflict),
            ChargingSessionCommandError.RobotNotAvailable => Problem(
                type: "ROBOTS_NOT_AVAILABLE",
                title: "Only an idle (AVAILABLE) robot can be sent to charge.",
                statusCode: StatusCodes.Status409Conflict),
            ChargingSessionCommandError.ActiveSessionExists => Problem(
                type: "ROBOT_CHARGING_SESSION_IN_PROGRESS",
                title: "The robot already has an active charging session.",
                statusCode: StatusCodes.Status409Conflict),
            ChargingSessionCommandError.StationNotFound or ChargingSessionCommandError.FloorMismatch or ChargingSessionCommandError.StationUnavailable => Problem(
                type: "CHARGING_STATION_UNAVAILABLE",
                title: "The configured charging station is unavailable.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Charging session service returned an invalid queue result.")
        };
    }

    /// <summary>
    /// Puts a robot into maintenance mode (SC-14). Operator or Admin.
    /// Refused with 409 if robot is on an active trip or already in maintenance.
    /// </summary>
    [HttpPost("{id:guid}/maintenance")]
    [Authorize(Policy = AuthPolicies.OperatorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MaintenanceRobot(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.MaintenanceRobotAsync(actorUserId, id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return result.Error switch
        {
            MaintenanceRobotError.NotFound             => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            MaintenanceRobotError.OnActiveTrip         => Problem(
                type: "ROBOTS_ON_ACTIVE_TRIP",
                title: "Cannot put a robot into maintenance while it is on an active trip.",
                statusCode: StatusCodes.Status409Conflict),
            MaintenanceRobotError.AlreadyInMaintenance => Problem(
                type: "ROBOTS_ALREADY_IN_MAINTENANCE",
                title: "The robot is already in maintenance mode.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot service returned an invalid maintenance result.")
        };
    }

    /// <summary>
    /// Returns a robot from maintenance back to service (SC-14). Operator or Admin.
    /// Robot must be in MAINTENANCE state.
    /// </summary>
    [HttpPost("{id:guid}/back-to-service")]
    [Authorize(Policy = AuthPolicies.OperatorOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BackToService(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId)) return Unauthorized();
        var result = await robotService.BackToServiceAsync(actorUserId, id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return result.Error switch
        {
            BackToServiceRobotError.NotFound         => Problem(
                type: "ROBOTS_NOT_FOUND",
                title: "The requested robot was not found.",
                statusCode: StatusCodes.Status404NotFound),
            BackToServiceRobotError.NotInMaintenance => Problem(
                type: "ROBOTS_NOT_IN_MAINTENANCE",
                title: "The robot is not currently in maintenance mode.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException("Robot service returned an invalid back-to-service result.")
        };
    }
    private bool TryGetActor(out Guid userId) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
