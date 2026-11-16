namespace RobotMissionMS.Business.Features.Plans.Services;

public interface IPlanCommandService
{
    Task<PlanCommandResult> CreateFallbackAsync(Guid userId, string floor, CancellationToken cancellationToken = default);
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

public enum PlanCommandError { InvalidRequest, FloorNotFound, FloorInactive, QueueConfigurationInvalid }
