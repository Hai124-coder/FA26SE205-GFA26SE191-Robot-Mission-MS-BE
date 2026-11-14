using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;

namespace RobotMissionMS.Business.Features.Users.Services;

public interface IUserService
{
    Task<UserListResult> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default);
    Task<CreateUserResult> CreateUserAsync(Guid createdByUserId, CreateUserRequest request,
        CancellationToken cancellationToken = default);
    Task<UpdateUserResult> UpdateUserAsync(Guid updatedByUserId, Guid userId, UpdateUserRequest request,
        CancellationToken cancellationToken = default);
    Task<UpdateUserStatusResult> UpdateUserStatusAsync(Guid updatedByUserId, Guid userId,
        UpdateUserStatusRequest request, CancellationToken cancellationToken = default);
    Task<ResetUserPasswordResult> ResetUserPasswordAsync(Guid resetByUserId, Guid userId,
        ResetUserPasswordRequest request, CancellationToken cancellationToken = default);
}
