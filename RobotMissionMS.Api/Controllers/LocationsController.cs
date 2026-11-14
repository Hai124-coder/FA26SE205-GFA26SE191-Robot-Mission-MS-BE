using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Business.Features.Locations.DTOs;
using RobotMissionMS.Business.Features.Locations.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/locations")]
[Authorize]
public sealed class LocationsController(ILocationService locationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LocationResponse>>> GetLocations([FromQuery] LocationListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await locationService.GetLocationsAsync(query, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            LocationListError.InvalidQuery => Problem(type: "LOCATIONS_INVALID_QUERY", title: "The location query is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException("Location service returned an invalid list result.")
        };
    }
}
