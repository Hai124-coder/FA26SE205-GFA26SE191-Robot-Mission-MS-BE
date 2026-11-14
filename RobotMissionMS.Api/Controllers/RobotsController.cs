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
}
