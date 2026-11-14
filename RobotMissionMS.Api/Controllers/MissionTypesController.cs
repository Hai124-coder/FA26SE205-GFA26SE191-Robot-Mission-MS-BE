using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
