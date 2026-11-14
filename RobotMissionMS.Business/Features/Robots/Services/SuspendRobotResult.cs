namespace RobotMissionMS.Business.Features.Robots.Services;

public enum SuspendRobotError
{
    NotFound,
    AlreadySuspended,
}

public sealed class SuspendRobotResult
{
    public bool IsSuccess { get; private init; }
    public SuspendRobotError? Error { get; private init; }

    public static SuspendRobotResult Success() => new() { IsSuccess = true };
    public static SuspendRobotResult Failure(SuspendRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
