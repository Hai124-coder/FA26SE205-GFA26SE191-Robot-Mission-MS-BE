using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class UserListResult
{
    private UserListResult(PagedResponse<UserListItemResponse>? response, UserListError? error)
    {
        Response = response;
        Error = error;
    }

    public PagedResponse<UserListItemResponse>? Response { get; }

    public UserListError? Error { get; }

    public bool IsSuccess => Response is not null;

    public static UserListResult Success(PagedResponse<UserListItemResponse> response) => new(response, null);

    public static UserListResult Failure(UserListError error) => new(null, error);
}

public enum UserListError
{
    InvalidQuery
}
