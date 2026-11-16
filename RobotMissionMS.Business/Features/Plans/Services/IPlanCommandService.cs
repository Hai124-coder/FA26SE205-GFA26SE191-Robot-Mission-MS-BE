namespace RobotMissionMS.Business.Features.Plans.Services;

using RobotMissionMS.Business.Features.Plans.DTOs;

public interface IPlanCommandService
{
    Task<PlanCommandResult> CreateFallbackAsync(Guid userId, string floor, CancellationToken cancellationToken = default);
    Task<PlanCommandResult> ReassignTaskAsync(Guid userId, string floor, Guid taskId, Guid? robotId, long version, CancellationToken cancellationToken = default);
    Task<PlanCommandResult> ChangeTaskPriorityAsync(Guid userId, string floor, Guid taskId, string priority, long version, CancellationToken cancellationToken = default);
    Task<PlanCommandResult> RemoveTripAsync(Guid userId, string floor, Guid robotId, long version, CancellationToken cancellationToken = default);
    Task<PlanCommandResult> DiscardCurrentAsync(Guid userId, string floor, long version, CancellationToken cancellationToken = default);
    Task<PlanCommandResult> ReleaseCurrentAsync(Guid userId, string floor, long version, CancellationToken cancellationToken = default);
}

public sealed class PlanCommandResult
{
    private PlanCommandResult(Guid? planId, PlanCommandError? error) { PlanId = planId; Error = error; }
    public Guid? PlanId { get; }
    public PlanCommandError? Error { get; }
    public bool IsSuccess => PlanId is not null;
    public static PlanCommandResult Success(Guid planId) => new(planId, null);
    public static PlanCommandResult Failure(PlanCommandError error) => new(null, error);
}

public enum PlanCommandError { InvalidRequest, FloorNotFound, FloorInactive, QueueConfigurationInvalid, NoCurrentPlan, TaskNotFound, FloorForbidden, WrongRobotType, PickupMismatch, SlotFull, RobotUnavailable, MapNotPublished, VersionConflict, PlanStale, NoAssignedTasks }
