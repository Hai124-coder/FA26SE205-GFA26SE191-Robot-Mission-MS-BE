namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class ResetUserPasswordResult
{
    private ResetUserPasswordResult(ResetUserPasswordError? error) => Error = error;

    public ResetUserPasswordError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ResetUserPasswordResult Success() => new(null);

    public static ResetUserPasswordResult Failure(ResetUserPasswordError error) => new(error);
}

public enum ResetUserPasswordError
{
    InvalidRequest,
    UserNotFound
}
