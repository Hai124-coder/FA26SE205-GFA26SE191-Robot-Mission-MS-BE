using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Floors.DTOs;
using RobotMissionMS.Business.Features.Floors.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/floors")]
[Authorize]
public sealed class FloorsController(IFloorService floorService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FloorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<FloorResponse>>> GetFloors(CancellationToken cancellationToken) =>
        Ok(await floorService.GetFloorsAsync(cancellationToken));

    [HttpGet("{floorCode}/map")]
    [ProducesResponseType(typeof(FloorMapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorMapResponse>> GetPublishedMap(string floorCode, CancellationToken cancellationToken)
    {
        var result = await floorService.GetPublishedMapAsync(floorCode, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            FloorMapError.FloorNotFound => Problem(type: "FLOORS_NOT_FOUND", title: "The requested floor was not found.",
                statusCode: StatusCodes.Status404NotFound),
            FloorMapError.MapNotPublished => Problem(type: "MAP_NOT_PUBLISHED", title: "The floor has no published map.",
                statusCode: StatusCodes.Status404NotFound),
            _ => throw new InvalidOperationException("Floor service returned an invalid map result.")
        };
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(FloorResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<FloorResponse>> CreateFloor([FromBody] CreateFloorRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await floorService.CreateFloorAsync(userId, request, cancellationToken);
        if (result.IsSuccess) return CreatedAtAction(nameof(GetFloors), result.Response);
        return result.Error switch
        {
            CreateFloorError.InvalidRequest => Problem(type: "FLOORS_INVALID_REQUEST", title: "The floor request is invalid.", statusCode: 400),
            CreateFloorError.DuplicateCode => Problem(type: "FLOORS_DUPLICATE_CODE", title: "The floor code already exists.", statusCode: 409),
            _ => throw new InvalidOperationException("Floor service returned an invalid create result.")
        };
    }

    [HttpPut("{floorCode}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(FloorResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FloorResponse>> UpdateFloor(string floorCode, [FromBody] UpdateFloorRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await floorService.UpdateFloorAsync(userId, floorCode, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            UpdateFloorError.InvalidRequest => Problem(type: "FLOORS_INVALID_REQUEST", title: "The floor request is invalid.", statusCode: 400),
            UpdateFloorError.NotFound => Problem(type: "FLOORS_NOT_FOUND", title: "The requested floor was not found.", statusCode: 404),
            UpdateFloorError.MapNotPublished => Problem(type: "MAP_NOT_PUBLISHED", title: "The floor has no published map.", statusCode: 409),
            _ => throw new InvalidOperationException("Floor service returned an invalid update result.")
        };
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
