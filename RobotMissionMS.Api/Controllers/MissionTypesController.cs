using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.MissionTypes.DTOs;
using RobotMissionMS.Business.Features.MissionTypes.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/mission-types")]
[Authorize]
public sealed class MissionTypesController(IMissionTypeService missionTypeService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MissionTypeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MissionTypeResponse>>> GetMissionTypes(CancellationToken cancellationToken) =>
        Ok(await missionTypeService.GetMissionTypesAsync(cancellationToken));

    [HttpPut("{type}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<MissionTypeResponse>> UpdateMissionType(string type, [FromBody] UpdateMissionTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)) return Unauthorized();
        var result = await missionTypeService.UpdateAsync(userId, type, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            UpdateMissionTypeError.InvalidRequest => Problem(type: "MISSION_TYPES_INVALID_REQUEST", title: "The mission type request is invalid.", statusCode: 400),
            UpdateMissionTypeError.NotFound => Problem(type: "MISSION_TYPES_NOT_FOUND", title: "The mission type was not found.", statusCode: 404),
            _ => throw new InvalidOperationException("Mission type service returned an invalid update result.")
        };
    }
}
