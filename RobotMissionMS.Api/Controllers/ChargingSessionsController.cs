using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.ChargingSessions.DTOs;
using RobotMissionMS.Business.Features.ChargingSessions.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/charging-sessions")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class ChargingSessionsController(IChargingSessionService sessions) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ChargingSessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PagedResponse<ChargingSessionResponse>>> List(
        [FromQuery] ChargingSessionListQuery query,
        CancellationToken cancellationToken)
    {
        var floor = User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
        if (User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(floor))
            return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: StatusCodes.Status409Conflict);

        var result = await sessions.ListAsync(query, floor, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Response)
            : Problem(type: "CHARGING_SESSIONS_INVALID_QUERY", statusCode: StatusCodes.Status400BadRequest);
    }
}
