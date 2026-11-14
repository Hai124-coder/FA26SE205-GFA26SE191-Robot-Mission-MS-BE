using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.Business.Features.Robots.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/robot-types")]
[Authorize]
public sealed class RobotTypesController(IRobotTypeService robotTypeService) : ControllerBase
{
    /// <summary>
    /// Returns all robot types (SC-05, SC-06).
    /// Accessible by every authenticated role: Operator, Supervisor, Administrator.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RobotTypeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RobotTypeResponse>>> GetRobotTypes(
        CancellationToken cancellationToken)
    {
        var result = await robotTypeService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates a robot type's editable fields (SC-05).
    /// Only Admin may call this endpoint.
    /// TypeCode is immutable and used only as a route key.
    /// </summary>
    [HttpPut("{type}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(RobotTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotTypeResponse>> UpdateRobotType(
        string type,
        [FromBody] UpdateRobotTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await robotTypeService.UpdateAsync(type, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            UpdateRobotTypeError.InvalidRequest => Problem(
                type: "ROBOT_TYPES_INVALID_UPDATE_REQUEST",
                title: "The robot type update request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            UpdateRobotTypeError.NotFound => Problem(
                type: "ROBOT_TYPES_NOT_FOUND",
                title: "The requested robot type was not found.",
                statusCode: StatusCodes.Status404NotFound),
            _ => throw new InvalidOperationException("Robot type service returned an invalid update result.")
        };
    }
}
