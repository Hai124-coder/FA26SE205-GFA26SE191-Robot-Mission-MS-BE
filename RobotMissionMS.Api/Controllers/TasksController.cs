using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Tasks.DTOs;
using RobotMissionMS.Business.Features.Tasks.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class TasksController(ITaskReadService tasks, ITaskCommandService commands) : ControllerBase
{
    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<TaskQueueItemResponse>>> Queue([FromQuery] TaskQueueQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await tasks.GetQueueAsync(query, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<TaskListItemResponse>>> List([FromQuery] TaskListQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await tasks.GetTasksAsync(query, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await tasks.GetTaskAsync(id, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<TaskTimelineResponse>> Timeline(Guid id, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        var result = await tasks.GetTimelineAsync(id, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<ActionResult<TaskActionResponse>> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
        => await Command(id, (userId, floor) => commands.UpdateAsync(id, userId, floor, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<ActionResult<TaskActionResponse>> Cancel(Guid id, [FromBody] CancelTaskRequest request, CancellationToken cancellationToken)
        => await Command(id, (userId, floor) => commands.CancelAsync(id, userId, floor, request, cancellationToken));

    [HttpPost("{id:guid}/redeliver")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<ActionResult<TaskActionResponse>> Redeliver(Guid id, CancellationToken cancellationToken)
        => await Command(id, (userId, floor) => commands.RedeliverAsync(id, userId, floor, cancellationToken));

    private string? OperatorFloor() => User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
    private bool OperatorWithoutFloor() => User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(OperatorFloor());
    private async Task<ActionResult<TaskActionResponse>> Command(Guid id, Func<Guid, string, Task<TaskCommandResult>> execute)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: 409);
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || OperatorFloor() is not { } floor) return Unauthorized();
        var result = await execute(userId, floor);
        return result.IsSuccess ? Ok(result.Response) : CommandFailure(result.Error!.Value);
    }
    private ActionResult ReadFailure(TaskReadError error) => error switch
    {
        TaskReadError.InvalidQuery => Problem(type: "TASKS_INVALID_QUERY", statusCode: 400),
        TaskReadError.NotFound => Problem(type: "TASK_NOT_FOUND", statusCode: 404),
        TaskReadError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", statusCode: 403),
        _ => throw new InvalidOperationException("Task service returned an invalid read result.")
    };
    private ActionResult CommandFailure(TaskCommandError error) => error switch
    {
        TaskCommandError.InvalidRequest => Problem(type: "TASKS_INVALID_REQUEST", statusCode: 400),
        TaskCommandError.NotFound => Problem(type: "TASK_NOT_FOUND", statusCode: 404),
        TaskCommandError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", statusCode: 403),
        TaskCommandError.NotPending => Problem(type: "TASK_NOT_PENDING", statusCode: 409),
        TaskCommandError.CargoNotReturned => Problem(type: "TASK_CARGO_NOT_RETURNED", statusCode: 409),
        TaskCommandError.ItemConfigurationInvalid => Problem(type: "TASK_ITEM_CONFIGURATION_INVALID", statusCode: 409),
        _ => throw new InvalidOperationException("Task command service returned an invalid result.")
    };
}
