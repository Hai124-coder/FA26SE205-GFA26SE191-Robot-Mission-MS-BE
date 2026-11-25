using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public interface IRobotControlCommandService
{
    Task<RobotControlCommandResult> EnqueueAsync(
        Guid actorUserId,
        Guid robotId,
        string? operatorFloor,
        RobotControlCommandRequest request,
        CancellationToken cancellationToken = default);
}

public enum RobotControlCommandError
{
    InvalidRequest,
    RobotNotFound,
    FloorForbidden,
    RobotNotConnected,
}

public sealed record RobotControlCommandResult(
    RobotControlCommandResponse? Response,
    RobotControlCommandError? Error)
{
    public bool IsSuccess => Error is null;

    public static RobotControlCommandResult Success(RobotControlCommandResponse response) => new(response, null);
    public static RobotControlCommandResult Failure(RobotControlCommandError error) => new(null, error);
}
