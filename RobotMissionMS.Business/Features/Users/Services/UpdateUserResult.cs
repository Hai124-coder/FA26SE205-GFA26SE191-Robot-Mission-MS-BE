using RobotMissionMS.Business.Features.Users.DTOs;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class UpdateUserResult
{
    private UpdateUserResult(UserListItemResponse? response, UpdateUserError? error)
    {
        Response = response;
        Error = error;
    }

    public UserListItemResponse? Response { get; }

    public UpdateUserError? Error { get; }

    public bool IsSuccess => Response is not null;

    public static UpdateUserResult Success(UserListItemResponse response) => new(response, null);

    public static UpdateUserResult Failure(UpdateUserError error) => new(null, error);
}

public enum UpdateUserError
{
    InvalidRequest,
    UserNotFound,
    RoleNotFound
}
