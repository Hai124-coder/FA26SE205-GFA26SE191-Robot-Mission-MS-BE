using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Plans.DTOs;
using RobotMissionMS.Business.Features.Plans.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/trips")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class TripsController(IPlanReadService plans) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TripListItemResponse>>> List([FromQuery] TripListQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await plans.GetTripsAsync(query, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TripDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await plans.GetTripAsync(id, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    private string? OperatorFloor() => User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
    private bool OperatorWithoutFloor() => User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(OperatorFloor());
    private static ActionResult Failure(PlanReadError error) => error switch
    {
        PlanReadError.InvalidQuery => new ObjectResult(new ProblemDetails { Type = "TRIPS_INVALID_QUERY", Status = 400 }) { StatusCode = 400 },
        PlanReadError.NotFound => new ObjectResult(new ProblemDetails { Type = "TRIP_NOT_FOUND", Status = 404 }) { StatusCode = 404 },
        PlanReadError.FloorForbidden => new ObjectResult(new ProblemDetails { Type = "FLOOR_FORBIDDEN", Status = 403 }) { StatusCode = 403 },
        _ => throw new InvalidOperationException("Plan service returned an invalid read result.")
    };
}
