using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;

namespace RobotMissionMS.Business.Features.Users.Services;

public interface IUserService
{
    Task<UserListResult> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default);
}
