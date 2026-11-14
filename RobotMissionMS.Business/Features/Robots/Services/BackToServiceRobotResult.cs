namespace RobotMissionMS.Business.Features.Robots.Services;

public enum BackToServiceRobotError
{
    NotFound,
    NotInMaintenance,
}

public sealed class BackToServiceRobotResult
{
    public bool IsSuccess { get; private init; }
    public BackToServiceRobotError? Error { get; private init; }

    public static BackToServiceRobotResult Success() => new() { IsSuccess = true };
    public static BackToServiceRobotResult Failure(BackToServiceRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
