using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Tasks.DTOs;

namespace RobotMissionMS.Business.Features.Tasks.Services;

public interface ITaskReadService
{
    Task<TaskListResult> GetTasksAsync(TaskListQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<TaskQueueResult> GetQueueAsync(TaskQueueQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<TaskDetailResult> GetTaskAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<TaskTimelineResult> GetTimelineAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default);
}

public sealed class TaskListResult
{
    private TaskListResult(PagedResponse<TaskListItemResponse>? response, TaskReadError? error) { Response = response; Error = error; }
    public PagedResponse<TaskListItemResponse>? Response { get; }
    public TaskReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static TaskListResult Success(PagedResponse<TaskListItemResponse> response) => new(response, null);
    public static TaskListResult Failure(TaskReadError error) => new(null, error);
}

public sealed class TaskDetailResult
{
    private TaskDetailResult(TaskDetailResponse? response, TaskReadError? error) { Response = response; Error = error; }
    public TaskDetailResponse? Response { get; }
    public TaskReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static TaskDetailResult Success(TaskDetailResponse response) => new(response, null);
    public static TaskDetailResult Failure(TaskReadError error) => new(null, error);
}

public sealed class TaskQueueResult
{
    private TaskQueueResult(IReadOnlyList<TaskQueueItemResponse>? response, TaskReadError? error) { Response = response; Error = error; }
    public IReadOnlyList<TaskQueueItemResponse>? Response { get; }
    public TaskReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static TaskQueueResult Success(IReadOnlyList<TaskQueueItemResponse> response) => new(response, null);
    public static TaskQueueResult Failure(TaskReadError error) => new(null, error);
}

public sealed class TaskTimelineResult
{
    private TaskTimelineResult(TaskTimelineResponse? response, TaskReadError? error) { Response = response; Error = error; }
    public TaskTimelineResponse? Response { get; }
    public TaskReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static TaskTimelineResult Success(TaskTimelineResponse response) => new(response, null);
    public static TaskTimelineResult Failure(TaskReadError error) => new(null, error);
}

public enum TaskReadError { InvalidQuery, NotFound, FloorForbidden }
