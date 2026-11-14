using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.Business.Features.Robots.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/robots")]
[Authorize]
public sealed class RobotsController(IRobotService robotService) : ControllerBase
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
        var result = await robotService.CreateRobotAsync(request, cancellationToken);
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
        var result = await robotService.UpdateRobotAsync(id, request, cancellationToken);
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
}
