namespace RobotMissionMS.Business.Features.Users.Services;

using RobotMissionMS.Business.Features.Users.DTOs;

public sealed class ResetUserPasswordResult
{
    private ResetUserPasswordResult(UserListItemResponse? response, ResetUserPasswordError? error) { Response = response; Error = error; }

    public UserListItemResponse? Response { get; }

    public ResetUserPasswordError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ResetUserPasswordResult Success(UserListItemResponse response) => new(response, null);

    public static ResetUserPasswordResult Failure(ResetUserPasswordError error) => new(null, error);
}

public enum ResetUserPasswordError
{
    InvalidRequest,
    UserNotFound,
    CannotResetSelf
}
