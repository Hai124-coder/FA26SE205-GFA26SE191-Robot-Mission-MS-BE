using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;
using RobotMissionMS.Business.Features.Auth.Security;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class UserService(IUserRepository repository, IPasswordService passwords, TimeProvider clock) : IUserService
{
    public async Task<ResetUserPasswordResult> ResetUserPasswordAsync(Guid resetByUserId, Guid userId,
        ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (resetByUserId == Guid.Empty || userId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.TemporaryPassword))
            return ResetUserPasswordResult.Failure(ResetUserPasswordError.InvalidRequest);

        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return ResetUserPasswordResult.Failure(ResetUserPasswordError.UserNotFound);

        var resetAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var userForHash = new User { Id = user.Id, Username = user.Username };
        var dataResult = await repository.ResetUserPasswordAsync(
            userId,
            passwords.HashPassword(userForHash, request.TemporaryPassword),
            resetByUserId,
            resetAt.UtcDateTime,
            cancellationToken);
        if (!dataResult.IsSuccess)
        {
            return dataResult.Error switch
            {
                UserPasswordResetDataError.UserNotFound => ResetUserPasswordResult.Failure(ResetUserPasswordError.UserNotFound),
                _ => throw new InvalidOperationException("User repository returned an invalid password-reset result.")
            };
        }

        return ResetUserPasswordResult.Success();
    }

    public async Task<UpdateUserStatusResult> UpdateUserStatusAsync(Guid updatedByUserId, Guid userId,
        UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (updatedByUserId == Guid.Empty || userId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || request.Active is null)
            return UpdateUserStatusResult.Failure(UpdateUserStatusError.InvalidRequest);

        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return UpdateUserStatusResult.Failure(UpdateUserStatusError.UserNotFound);

        var updatedAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var dataResult = await repository.UpdateUserStatusAsync(
            userId, request.Active.Value, updatedByUserId, updatedAt.UtcDateTime, cancellationToken);
        if (!dataResult.IsSuccess)
        {
            return dataResult.Error switch
            {
                UserStatusUpdateDataError.UserNotFound => UpdateUserStatusResult.Failure(UpdateUserStatusError.UserNotFound),
                _ => throw new InvalidOperationException("User repository returned an invalid status result.")
            };
        }

        return UpdateUserStatusResult.Success(new UserListItemResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = request.Active.Value,
            CreatedAt = new DateTimeOffset(user.CreatedAt)
        });
    }

    public async Task<UpdateUserResult> UpdateUserAsync(Guid updatedByUserId, Guid userId, UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (updatedByUserId == Guid.Empty || userId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Role))
            return UpdateUserResult.Failure(UpdateUserError.InvalidRequest);

        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return UpdateUserResult.Failure(UpdateUserError.UserNotFound);

        var role = await repository.FindRoleByNameAsync(request.Role, cancellationToken);
        if (role is null)
            return UpdateUserResult.Failure(UpdateUserError.RoleNotFound);

        var updatedAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var dataResult = await repository.UpdateUserAsync(
            userId, request.FullName, role.Id, updatedByUserId, updatedAt.UtcDateTime, cancellationToken);
        if (!dataResult.IsSuccess)
        {
            return dataResult.Error switch
            {
                UserUpdateDataError.UserNotFound => UpdateUserResult.Failure(UpdateUserError.UserNotFound),
                _ => throw new InvalidOperationException("User repository returned an invalid update result.")
            };
        }

        return UpdateUserResult.Success(new UserListItemResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = request.FullName,
            Email = user.Email,
            Role = role.Name,
            IsActive = user.IsActive,
            CreatedAt = new DateTimeOffset(user.CreatedAt)
        });
    }

    public async Task<CreateUserResult> CreateUserAsync(Guid createdByUserId, CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (createdByUserId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Role)
            || string.IsNullOrWhiteSpace(request.TemporaryPassword))
            return CreateUserResult.Failure(CreateUserError.InvalidRequest);

        if (await repository.UsernameExistsAsync(request.Username, cancellationToken))
            return CreateUserResult.Failure(CreateUserError.UsernameAlreadyExists);

        var role = await repository.FindRoleByNameAsync(request.Role, cancellationToken);
        if (role is null)
            return CreateUserResult.Failure(CreateUserError.RoleNotFound);

        var createdAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var id = Guid.NewGuid();
        var userForHash = new User { Id = id, Username = request.Username };
        var dataResult = await repository.CreateUserAsync(new UserCreateRecord(
                id,
                request.Username,
                passwords.HashPassword(userForHash, request.TemporaryPassword),
                request.FullName,
                request.Email,
                role.Id,
                createdAt.UtcDateTime),
            createdByUserId,
            createdAt.UtcDateTime,
            cancellationToken);

        if (!dataResult.IsSuccess)
        {
            return dataResult.Error switch
            {
                UserCreateDataError.UsernameAlreadyExists => CreateUserResult.Failure(CreateUserError.UsernameAlreadyExists),
                _ => throw new InvalidOperationException("User repository returned an invalid create result.")
            };
        }

        return CreateUserResult.Success(new UserListItemResponse
        {
            Id = id,
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            Role = role.Name,
            IsActive = true,
            CreatedAt = createdAt
        });
    }

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

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc));
    }
}
