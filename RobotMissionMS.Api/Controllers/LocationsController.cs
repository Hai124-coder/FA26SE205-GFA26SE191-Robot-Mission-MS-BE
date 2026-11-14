using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
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

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(LocationResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<LocationResponse>> CreateLocation([FromBody] CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await locationService.CreateLocationAsync(userId, request, cancellationToken);
        if (result.IsSuccess) return Created($"/api/v1/locations/{result.Response!.Code}", result.Response);
        return result.Error switch
        {
            CreateLocationError.InvalidRequest => Problem(type: "LOCATIONS_INVALID_REQUEST", title: "The location request is invalid.", statusCode: 400),
            CreateLocationError.DuplicateCode => Problem(type: "LOCATIONS_DUPLICATE_CODE", title: "The location code already exists.", statusCode: 409),
            CreateLocationError.FloorNotFound => Problem(type: "FLOORS_NOT_FOUND", title: "The specified floor was not found.", statusCode: 400),
            CreateLocationError.FloorInactive => Problem(type: "FLOORS_INACTIVE", title: "The specified floor is inactive.", statusCode: 409),
            CreateLocationError.MapNodeNotFound => Problem(type: "MAP_NODE_NOT_FOUND", title: "The map node is not in the published floor map.", statusCode: 400),
            _ => throw new InvalidOperationException("Location service returned an invalid create result.")
        };
    }

    [HttpPut("{code}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<LocationResponse>> UpdateLocation(string code, [FromBody] UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await locationService.UpdateLocationAsync(userId, code, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            UpdateLocationError.InvalidRequest => Problem(type: "LOCATIONS_INVALID_REQUEST", title: "The location request is invalid.", statusCode: 400),
            UpdateLocationError.NotFound => Problem(type: "LOCATIONS_NOT_FOUND", title: "The requested location was not found.", statusCode: 404),
            UpdateLocationError.AmbiguousCode => Problem(type: "LOCATIONS_AMBIGUOUS_CODE", title: "The location code is not unique.", statusCode: 409),
            UpdateLocationError.FloorChangeNotAllowed => Problem(type: "LOCATIONS_FLOOR_CHANGE_NOT_ALLOWED", title: "Moving a location between floors is not supported.", statusCode: 409),
            UpdateLocationError.MapNodeNotFound => Problem(type: "MAP_NODE_NOT_FOUND", title: "The map node is not in the published floor map.", statusCode: 400),
            _ => throw new InvalidOperationException("Location service returned an invalid update result.")
        };
    }

    [HttpPatch("{code}/status")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<LocationResponse>> UpdateLocationStatus(string code, [FromBody] UpdateLocationStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await locationService.UpdateLocationStatusAsync(userId, code, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        if (result.Error == UpdateLocationStatusError.InUse)
        {
            var details = new ProblemDetails { Type = "LOCATIONS_IN_USE", Title = "The location is still in use.", Status = 409 };
            details.Extensions["references"] = result.Usage;
            return Conflict(details);
        }
        return result.Error switch
        {
            UpdateLocationStatusError.InvalidRequest => Problem(type: "LOCATIONS_INVALID_STATUS_REQUEST", title: "The location status request is invalid.", statusCode: 400),
            UpdateLocationStatusError.NotFound => Problem(type: "LOCATIONS_NOT_FOUND", title: "The requested location was not found.", statusCode: 404),
            UpdateLocationStatusError.AmbiguousCode => Problem(type: "LOCATIONS_AMBIGUOUS_CODE", title: "The location code is not unique.", statusCode: 409),
            _ => throw new InvalidOperationException("Location service returned an invalid status result.")
        };
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
