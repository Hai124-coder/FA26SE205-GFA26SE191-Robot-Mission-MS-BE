using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    /// Note: per-Operator floor restriction requires a User→Zone assignment column
    /// which is not yet in the schema; callers may pass ?floor= to self-filter.
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
}
