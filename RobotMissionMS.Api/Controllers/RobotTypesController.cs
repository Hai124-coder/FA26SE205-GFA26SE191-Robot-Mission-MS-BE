using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
