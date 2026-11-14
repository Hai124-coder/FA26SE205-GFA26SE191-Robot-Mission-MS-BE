using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
