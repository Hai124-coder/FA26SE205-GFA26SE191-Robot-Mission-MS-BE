using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;
using RobotMissionMS.Business.Features.Auth.Security;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Repositories.Interfaces;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Users.Services;

public sealed class UserService(IUserRepository repository, IPasswordService passwords, ITemporaryPasswordGenerator temporaryPasswords, TimeProvider clock) : IUserService
{
    public async Task<ResetUserPasswordResult> ResetUserPasswordAsync(Guid resetByUserId, Guid userId,
        ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (resetByUserId == Guid.Empty || userId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true))
            return ResetUserPasswordResult.Failure(ResetUserPasswordError.InvalidRequest);

        if (resetByUserId == userId) return ResetUserPasswordResult.Failure(ResetUserPasswordError.CannotResetSelf);

        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return ResetUserPasswordResult.Failure(ResetUserPasswordError.UserNotFound);

        var resetAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var temporaryPassword = temporaryPasswords.Generate();
        var userForHash = new User { Id = user.Id, Username = user.Username };
        var dataResult = await repository.ResetUserPasswordAsync(
            userId,
            passwords.HashPassword(userForHash, temporaryPassword),
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

        return ResetUserPasswordResult.Success(ToResponse(user, mustChangePassword: true, hasOpenResetRequest: false, tempPassword: temporaryPassword));
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

        return UpdateUserStatusResult.Success(ToResponse(user, isActive: request.Active.Value));
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
        var floorResult = await ResolveFloorAsync(role.Name, request.Floor, cancellationToken);
        if (floorResult.Error is not null) return UpdateUserResult.Failure(floorResult.Error.Value);

        var updatedAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var dataResult = await repository.UpdateUserAsync(
            userId, request.FullName, role.Id, floorResult.ZoneId, updatedByUserId, updatedAt.UtcDateTime, cancellationToken);
        if (!dataResult.IsSuccess)
        {
            return dataResult.Error switch
            {
                UserUpdateDataError.UserNotFound => UpdateUserResult.Failure(UpdateUserError.UserNotFound),
                _ => throw new InvalidOperationException("User repository returned an invalid update result.")
            };
        }

        return UpdateUserResult.Success(ToResponse(user, fullName: request.FullName, role: role.Name, floor: floorResult.FloorCode));
    }

    public async Task<CreateUserResult> CreateUserAsync(Guid createdByUserId, CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (createdByUserId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Role))
            return CreateUserResult.Failure(CreateUserError.InvalidRequest);

        if (await repository.UsernameExistsAsync(request.Username, cancellationToken))
            return CreateUserResult.Failure(CreateUserError.UsernameAlreadyExists);

        var role = await repository.FindRoleByNameAsync(request.Role, cancellationToken);
        if (role is null)
            return CreateUserResult.Failure(CreateUserError.RoleNotFound);
        var floorResult = await ResolveFloorAsync(role.Name, request.Floor, cancellationToken);
        if (floorResult.Error is not null) return CreateUserResult.Failure(floorResult.Error.Value switch
        {
            UpdateUserError.OperatorFloorRequired => CreateUserError.OperatorFloorRequired,
            UpdateUserError.NonOperatorFloorForbidden => CreateUserError.NonOperatorFloorForbidden,
            UpdateUserError.FloorNotFound => CreateUserError.FloorNotFound,
            _ => CreateUserError.FloorInactive
        });

        var createdAt = TruncateToMicroseconds(clock.GetUtcNow().ToUniversalTime());
        var id = Guid.NewGuid();
        var temporaryPassword = temporaryPasswords.Generate();
        var userForHash = new User { Id = id, Username = request.Username };
        var dataResult = await repository.CreateUserAsync(new UserCreateRecord(
                id,
                request.Username,
                passwords.HashPassword(userForHash, temporaryPassword),
                request.FullName,
                request.Email,
                role.Id,
                floorResult.ZoneId,
                true,
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

        return CreateUserResult.Success(new UserListItemResponse { Id = id, Username = request.Username, FullName = request.FullName, Email = request.Email, Role = role.Name, Floor = floorResult.FloorCode, IsActive = true, MustChangePassword = true, HasOpenResetRequest = false, LastLoginAt = null, TempPassword = temporaryPassword, CreatedAt = createdAt });
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
            floorCode: query.Floor,
            isActive: query.Active,
            cancellationToken);

        return UserListResult.Success(new PagedResponse<UserListItemResponse>
        {
            Items = page.Items.Select(user => ToResponse(user)).ToList(),
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

    private static UserListItemResponse ToResponse(UserListRow user, bool? isActive = null, bool? mustChangePassword = null, bool? hasOpenResetRequest = null, string? fullName = null, string? role = null, string? floor = null, string? tempPassword = null) => new()
    {
        Id = user.Id, Username = user.Username, FullName = fullName ?? user.FullName, Email = user.Email,
        Role = role ?? user.Role, Floor = floor ?? user.FloorCode, IsActive = isActive ?? user.IsActive,
        MustChangePassword = mustChangePassword ?? user.MustChangePassword, HasOpenResetRequest = hasOpenResetRequest ?? user.HasOpenResetRequest,
        LastLoginAt = user.LastLoginAt is null ? null : new DateTimeOffset(user.LastLoginAt.Value), TempPassword = tempPassword,
        CreatedAt = new DateTimeOffset(user.CreatedAt)
    };

    private async Task<FloorResolution> ResolveFloorAsync(string role, string? floor, CancellationToken cancellationToken)
    {
        var hasFloor = !string.IsNullOrWhiteSpace(floor);
        if (!string.Equals(role, "OPERATOR", StringComparison.OrdinalIgnoreCase))
            return hasFloor ? FloorResolution.Failure(UpdateUserError.NonOperatorFloorForbidden) : FloorResolution.Success(null, null);
        if (!hasFloor) return FloorResolution.Failure(UpdateUserError.OperatorFloorRequired);
        var zone = await repository.FindZoneByCodeAsync(floor!.Trim(), cancellationToken);
        if (zone is null) return FloorResolution.Failure(UpdateUserError.FloorNotFound);
        return zone.IsActive
            ? FloorResolution.Success(zone.Id, zone.Code)
            : FloorResolution.Failure(UpdateUserError.FloorInactive);
    }

    private sealed record FloorResolution(Guid? ZoneId, string? FloorCode, UpdateUserError? Error)
    {
        public static FloorResolution Success(Guid? zoneId, string? floorCode) => new(zoneId, floorCode, null);
        public static FloorResolution Failure(UpdateUserError error) => new(null, null, error);
    }
}
