namespace RobotMissionMS.Business.Features.Robots.Services;

public enum DeleteRobotError
{
    NotFound,
    RobotOnActiveTrip,
}

public sealed class DeleteRobotResult
{
    public bool IsSuccess { get; private init; }
    public DeleteRobotError? Error { get; private init; }

    public static DeleteRobotResult Success() =>
        new() { IsSuccess = true };
    public static DeleteRobotResult Failure(DeleteRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
