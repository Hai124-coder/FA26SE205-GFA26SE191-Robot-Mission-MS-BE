using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class UserService(IUserRepository repository) : IUserService
{
    public async Task<UserListResult> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, validateAllProperties: true)
            || (query.Sort is not null && !string.Equals(query.Sort, "createdAt:desc", StringComparison.Ordinal)))
            return UserListResult.Failure(UserListError.InvalidQuery);

        var page = await repository.GetUsersAsync(
            skip: (query.Page - 1) * query.PageSize,
            take: query.PageSize,
            role: query.Role,
            isActive: query.Active,
            cancellationToken);

        return UserListResult.Success(new PagedResponse<UserListItemResponse>
        {
            Items = page.Items.Select(user => new UserListItemResponse
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = new DateTimeOffset(user.CreatedAt)
            }).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            Total = page.Total
        });
    }
}
