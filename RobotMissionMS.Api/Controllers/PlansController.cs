using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Plans.DTOs;
using RobotMissionMS.Business.Features.Plans.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/plans")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class PlansController(IPlanReadService plans, IPlanCommandService commands) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<ActionResult<PlanDetailResponse>> Create(CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || OperatorFloor() is not { } floor) return Unauthorized();
        var created = await commands.CreateFallbackAsync(userId, floor, cancellationToken);
        if (!created.IsSuccess) return CommandFailure(created.Error!.Value);
        var result = await plans.GetPlanAsync(created.PlanId!.Value, floor, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.PlanId }, result.Response);
    }
    [HttpGet("current")]
    public async Task<ActionResult<PlanDetailResponse>> Current([FromQuery] PlanCurrentQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await plans.GetCurrentAsync(query, OperatorFloor(), cancellationToken);
        if (!result.IsSuccess) return Failure(result.Error!.Value);
        return result.Response is null ? NoContent() : Ok(result.Response);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<PlanSummaryResponse>>> List([FromQuery] PlanListQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await plans.GetPlansAsync(query, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await plans.GetPlanAsync(id, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    private string? OperatorFloor() => User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
    private bool OperatorWithoutFloor() => User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(OperatorFloor());
    private static ActionResult Failure(PlanReadError error) => error switch
    {
        PlanReadError.InvalidQuery => new ObjectResult(new ProblemDetails { Type = "PLANS_INVALID_QUERY", Status = 400 }) { StatusCode = 400 },
        PlanReadError.NotFound => new ObjectResult(new ProblemDetails { Type = "PLAN_NOT_FOUND", Status = 404 }) { StatusCode = 404 },
        PlanReadError.FloorForbidden => new ObjectResult(new ProblemDetails { Type = "FLOOR_FORBIDDEN", Status = 403 }) { StatusCode = 403 },
        _ => throw new InvalidOperationException("Plan service returned an invalid read result.")
    };
    private static ActionResult CommandFailure(PlanCommandError error) => error switch
    {
        PlanCommandError.InvalidRequest => new ObjectResult(new ProblemDetails { Type = "PLANS_INVALID_REQUEST", Status = 400 }) { StatusCode = 400 },
        PlanCommandError.FloorNotFound => new ObjectResult(new ProblemDetails { Type = "FLOOR_NOT_FOUND", Status = 404 }) { StatusCode = 404 },
        PlanCommandError.FloorInactive => new ObjectResult(new ProblemDetails { Type = "FLOOR_INACTIVE", Status = 409 }) { StatusCode = 409 },
        PlanCommandError.QueueConfigurationInvalid => new ObjectResult(new ProblemDetails { Type = "PLAN_QUEUE_CONFIGURATION_INVALID", Status = 409 }) { StatusCode = 409 },
        _ => throw new InvalidOperationException("Plan command service returned an invalid result.")
    };
}
