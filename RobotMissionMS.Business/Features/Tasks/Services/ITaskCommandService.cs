using RobotMissionMS.Business.Features.Tasks.DTOs;

namespace RobotMissionMS.Business.Features.Tasks.Services;

public interface ITaskCommandService
{
    Task<TaskCommandResult> UpdateAsync(Guid id, Guid userId, string floor, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskCommandResult> CancelAsync(Guid id, Guid userId, string floor, CancelTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskCommandResult> RedeliverAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
}

public sealed class TaskCommandResult
{
    private TaskCommandResult(TaskActionResponse? response, TaskCommandError? error) { Response = response; Error = error; }
    public TaskActionResponse? Response { get; }
    public TaskCommandError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static TaskCommandResult Success(TaskActionResponse response) => new(response, null);
    public static TaskCommandResult Failure(TaskCommandError error) => new(null, error);
}

public enum TaskCommandError { InvalidRequest, NotFound, FloorForbidden, NotPending, CargoNotReturned, ItemConfigurationInvalid }
