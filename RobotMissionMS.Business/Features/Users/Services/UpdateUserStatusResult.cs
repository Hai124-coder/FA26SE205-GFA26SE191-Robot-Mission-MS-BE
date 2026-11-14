using RobotMissionMS.Business.Features.Users.DTOs;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class UpdateUserStatusResult
{
    private UpdateUserStatusResult(UserListItemResponse? response, UpdateUserStatusError? error)
    {
        Response = response;
        Error = error;
    }

    public UserListItemResponse? Response { get; }

    public UpdateUserStatusError? Error { get; }

    public bool IsSuccess => Response is not null;

    public static UpdateUserStatusResult Success(UserListItemResponse response) => new(response, null);

    public static UpdateUserStatusResult Failure(UpdateUserStatusError error) => new(null, error);
}

public enum UpdateUserStatusError
{
    InvalidRequest,
    UserNotFound
}
