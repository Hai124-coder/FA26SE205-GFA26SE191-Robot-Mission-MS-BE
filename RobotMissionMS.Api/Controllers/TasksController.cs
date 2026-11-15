using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Tasks.DTOs;
using RobotMissionMS.Business.Features.Tasks.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class TasksController(ITaskReadService tasks) : ControllerBase
{
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

    private string? OperatorFloor() => User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
    private bool OperatorWithoutFloor() => User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(OperatorFloor());
    private ActionResult ReadFailure(TaskReadError error) => error switch
    {
        TaskReadError.InvalidQuery => Problem(type: "TASKS_INVALID_QUERY", statusCode: 400),
        TaskReadError.NotFound => Problem(type: "TASK_NOT_FOUND", statusCode: 404),
        TaskReadError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", statusCode: 403),
        _ => throw new InvalidOperationException("Task service returned an invalid read result.")
    };
}
