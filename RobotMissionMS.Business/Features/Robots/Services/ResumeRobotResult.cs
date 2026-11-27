namespace RobotMissionMS.Business.Features.Robots.Services;

public enum ResumeRobotError
{
    NotFound,
    NotSuspended,
    Offline,
}

public sealed class ResumeRobotResult
{
    public bool IsSuccess { get; private init; }
    public ResumeRobotError? Error { get; private init; }

    public static ResumeRobotResult Success() => new() { IsSuccess = true };
    public static ResumeRobotResult Failure(ResumeRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
