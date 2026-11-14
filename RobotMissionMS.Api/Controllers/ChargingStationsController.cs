using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
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

    [HttpPatch("{code}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ChargingStationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChargingStationResponse>> UpdateChargingStation(string code,
        [FromBody] UpdateChargingStationRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)) return Unauthorized();
        var result = await chargingStationService.UpdateChargingStationAsync(userId, code, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            UpdateChargingStationError.InvalidRequest => Problem(type: "CHARGING_STATIONS_INVALID_REQUEST", title: "The charging station request is invalid.", statusCode: 400),
            UpdateChargingStationError.NotFound => Problem(type: "CHARGING_STATIONS_NOT_FOUND", title: "The requested charging station was not found.", statusCode: 404),
            UpdateChargingStationError.AmbiguousCode => Problem(type: "CHARGING_STATIONS_AMBIGUOUS_CODE", title: "The charging station code is not unique.", statusCode: 409),
            UpdateChargingStationError.CapacityBelowActiveSessions => Problem(type: "CHARGING_STATIONS_CAPACITY_IN_USE", title: "Capacity cannot be lower than active charging sessions.", statusCode: 409),
            _ => throw new InvalidOperationException("Charging station service returned an invalid update result.")
        };
    }
}
