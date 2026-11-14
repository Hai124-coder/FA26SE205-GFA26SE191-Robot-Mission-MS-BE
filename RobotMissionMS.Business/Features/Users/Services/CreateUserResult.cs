using RobotMissionMS.Business.Features.Users.DTOs;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class CreateUserResult
{
    private CreateUserResult(UserListItemResponse? response, CreateUserError? error)
    {
        Response = response;
        Error = error;
    }

    public UserListItemResponse? Response { get; }

    public CreateUserError? Error { get; }

    public bool IsSuccess => Response is not null;

    public static CreateUserResult Success(UserListItemResponse response) => new(response, null);

    public static CreateUserResult Failure(CreateUserError error) => new(null, error);
}

public enum CreateUserError
{
    InvalidRequest,
    UsernameAlreadyExists,
    RoleNotFound
}
