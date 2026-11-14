using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Business.Features.ChargingStations.DTOs;
using RobotMissionMS.Business.Features.ChargingStations.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/charging-stations")]
[Authorize]
public sealed class ChargingStationsController(IChargingStationService chargingStationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChargingStationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ChargingStationResponse>>> GetChargingStations(
        CancellationToken cancellationToken) => Ok(await chargingStationService.GetChargingStationsAsync(cancellationToken));
}
