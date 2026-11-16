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
    [HttpPatch("current/tasks/{taskId:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public Task<ActionResult<PlanDetailResponse>> Reassign(Guid taskId, [FromBody] PlanTaskAssignmentRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
        => Modify(ifMatch, (userId, floor, version) => commands.ReassignTaskAsync(userId, floor, taskId, request.RobotId, version, cancellationToken));

    [HttpPatch("current/tasks/{taskId:guid}/priority")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public Task<ActionResult<PlanDetailResponse>> Priority(Guid taskId, [FromBody] PlanTaskPriorityRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
        => Modify(ifMatch, (userId, floor, version) => commands.ChangeTaskPriorityAsync(userId, floor, taskId, request.Priority ?? string.Empty, version, cancellationToken));

    [HttpDelete("current/trips/{robotId:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public Task<ActionResult<PlanDetailResponse>> RemoveTrip(Guid robotId, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
        => Modify(ifMatch, (userId, floor, version) => commands.RemoveTripAsync(userId, floor, robotId, version, cancellationToken));

    [HttpDelete("current")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Discard([FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (!TryCommandContext(ifMatch, out var userId, out var floor, out var version, out var error)) return error!;
        var result = await commands.DiscardCurrentAsync(userId, floor, version, cancellationToken);
        return result.IsSuccess ? NoContent() : CommandFailure(result.Error!.Value);
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
    private async Task<ActionResult<PlanDetailResponse>> Modify(string? ifMatch, Func<Guid, string, long, Task<PlanCommandResult>> action)
    {
        if (!TryCommandContext(ifMatch, out var userId, out var floor, out var version, out var error)) return error!;
        var changed = await action(userId, floor, version);
        if (!changed.IsSuccess) return CommandFailure(changed.Error!.Value);
        var result = await plans.GetPlanAsync(changed.PlanId!.Value, floor);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }
    private bool TryCommandContext(string? ifMatch, out Guid userId, out string floor, out long version, out ActionResult? error)
    {
        userId = Guid.Empty; floor = string.Empty; version = 0; error = null;
        if (OperatorWithoutFloor()) { error = Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409); return false; }
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId) || OperatorFloor() is not { } operatorFloor) { error = Unauthorized(); return false; }
        if (!long.TryParse(ifMatch?.Trim().Trim('"'), out version) || version <= 0) { error = Problem(type: "PLANS_IF_MATCH_REQUIRED", statusCode: 400); return false; }
        floor = operatorFloor; return true;
    }
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
        PlanCommandError.NoCurrentPlan => new ObjectResult(new ProblemDetails { Type = "PLAN_DRAFT_NOT_FOUND", Status = 404 }) { StatusCode = 404 },
        PlanCommandError.TaskNotFound => new ObjectResult(new ProblemDetails { Type = "PLAN_TASK_NOT_FOUND", Status = 404 }) { StatusCode = 404 },
        PlanCommandError.FloorForbidden => new ObjectResult(new ProblemDetails { Type = "FLOOR_FORBIDDEN", Status = 403 }) { StatusCode = 403 },
        PlanCommandError.WrongRobotType => new ObjectResult(new ProblemDetails { Type = "BR-13_WRONG_TYPE", Status = 409 }) { StatusCode = 409 },
        PlanCommandError.PickupMismatch => new ObjectResult(new ProblemDetails { Type = "BR-10_PICKUP_MISMATCH", Status = 409 }) { StatusCode = 409 },
        PlanCommandError.SlotFull => new ObjectResult(new ProblemDetails { Type = "BR-11_SLOT_FULL", Status = 409 }) { StatusCode = 409 },
        PlanCommandError.RobotUnavailable => new ObjectResult(new ProblemDetails { Type = "PLAN_ROBOT_UNAVAILABLE", Status = 409 }) { StatusCode = 409 },
        PlanCommandError.MapNotPublished => new ObjectResult(new ProblemDetails { Type = "PLAN_MAP_NOT_PUBLISHED", Status = 409 }) { StatusCode = 409 },
        PlanCommandError.VersionConflict => new ObjectResult(new ProblemDetails { Type = "PLAN_VERSION_CONFLICT", Status = 409 }) { StatusCode = 409 },
        _ => throw new InvalidOperationException("Plan command service returned an invalid result.")
    };
}
