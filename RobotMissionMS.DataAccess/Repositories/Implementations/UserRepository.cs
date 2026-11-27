using Microsoft.EntityFrameworkCore;
using Npgsql;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class UserRepository(RobotMissionDbContext db) : IUserRepository
{
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Username == username, cancellationToken);

    public Task<RoleRecord?> FindRoleByNameAsync(string roleName, CancellationToken cancellationToken = default) =>
        db.Roles.AsNoTracking()
            .Where(role => role.Name == roleName)
            .Select(role => new RoleRecord(role.Id, role.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserListRow?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserListRow(
                user.Id, user.Username, user.FullName, user.Email, user.Role.Name, user.Zone == null ? null : user.Zone.Code,
                user.IsActive, user.MustChangePassword,
                db.PasswordResetRequests.Any(request => request.UserId == user.Id && request.Status == RobotMissionMS.DataAccess.Enums.PasswordResetRequestStatus.Open),
                user.LastLoginAt, user.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ZoneRecord?> FindZoneByCodeAsync(string zoneCode, CancellationToken cancellationToken = default) =>
        db.Zones.AsNoTracking().Where(zone => zone.Code == zoneCode)
            .Select(zone => new ZoneRecord(zone.Id, zone.Code, zone.IsActive)).SingleOrDefaultAsync(cancellationToken);

    public async Task<UserListPage> GetUsersAsync(
        int skip,
        int take,
        string? role,
        string? floorCode,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(user => user.Role.Name == role);
        if (!string.IsNullOrWhiteSpace(floorCode))
            query = query.Where(user => user.Zone != null && user.Zone.Code == floorCode);
        if (isActive.HasValue)
            query = query.Where(user => user.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Take(take)
            .Select(user => new UserListRow(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role.Name,
                user.Zone == null ? null : user.Zone.Code,
                user.IsActive,
                user.MustChangePassword,
                db.PasswordResetRequests.Any(request => request.UserId == user.Id && request.Status == RobotMissionMS.DataAccess.Enums.PasswordResetRequestStatus.Open),
                user.LastLoginAt,
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        return new UserListPage(items, total);
    }

    public async Task<UserCreateDataResult> CreateUserAsync(
        UserCreateRecord user,
        Guid createdByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must be UTC.", nameof(occurredAtUtc));

        db.Users.Add(new User
        {
            Id = user.Id,
            Username = user.Username,
            PasswordHash = user.PasswordHash,
            FullName = user.FullName,
            Email = user.Email,
            IsActive = true,
            MustChangePassword = user.MustChangePassword,
            RoleId = user.RoleId,
            ZoneId = user.ZoneId,
            CreatedAt = user.CreatedAt
        });
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = createdByUserId,
            Timestamp = occurredAtUtc,
            Action = "USER_CREATED",
            TargetObject = $"User:{user.Id:D}"
        });

        try
        {
            // EF wraps this SaveChanges call in a transaction, so the user and
            // audit entry either both persist or neither does.
            await db.SaveChangesAsync(cancellationToken);
            return UserCreateDataResult.Success();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "Users_Username_key" })
        {
            db.ChangeTracker.Clear();
            return UserCreateDataResult.Failure(UserCreateDataError.UsernameAlreadyExists);
        }
    }

    public async Task<UserUpdateDataResult> UpdateUserAsync(
        Guid userId,
        string fullName,
        Guid roleId,
        Guid? zoneId,
        Guid updatedByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must be UTC.", nameof(occurredAtUtc));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.Users.Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.FullName, fullName)
                .SetProperty(user => user.RoleId, roleId)
                .SetProperty(user => user.ZoneId, zoneId), cancellationToken);
        if (updated == 0)
            return UserUpdateDataResult.Failure(UserUpdateDataError.UserNotFound);

        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = updatedByUserId,
            Timestamp = occurredAtUtc,
            Action = "USER_UPDATED",
            TargetObject = $"User:{userId:D}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserUpdateDataResult.Success();
    }

    public async Task<UserStatusUpdateDataResult> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        Guid updatedByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must be UTC.", nameof(occurredAtUtc));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.Users.Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsActive, isActive), cancellationToken);
        if (updated == 0)
            return UserStatusUpdateDataResult.Failure(UserStatusUpdateDataError.UserNotFound);

        if (!isActive)
        {
            await db.RefreshTokens.Where(token => token.UserId == userId && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, occurredAtUtc), cancellationToken);
        }

        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = updatedByUserId,
            Timestamp = occurredAtUtc,
            Action = "USER_STATUS_CHANGED",
            TargetObject = $"User:{userId:D};Active:{isActive}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserStatusUpdateDataResult.Success();
    }

    public async Task<UserPasswordResetDataResult> ResetUserPasswordAsync(
        Guid userId,
        string passwordHash,
        Guid resetByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (passwordHash.Length > 255)
            throw new ArgumentException("Password hash exceeds the database column limit.", nameof(passwordHash));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must be UTC.", nameof(occurredAtUtc));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.Users.Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.PasswordHash, passwordHash)
                .SetProperty(user => user.MustChangePassword, true), cancellationToken);
        if (updated == 0)
            return UserPasswordResetDataResult.Failure(UserPasswordResetDataError.UserNotFound);

        await db.RefreshTokens.Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, occurredAtUtc), cancellationToken);
        await db.PasswordResetRequests.Where(request => request.UserId == userId && request.Status == RobotMissionMS.DataAccess.Enums.PasswordResetRequestStatus.Open)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(request => request.Status, RobotMissionMS.DataAccess.Enums.PasswordResetRequestStatus.Done)
                .SetProperty(request => request.ProcessedAt, occurredAtUtc)
                .SetProperty(request => request.ProcessedByUserId, resetByUserId), cancellationToken);
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = resetByUserId,
            Timestamp = occurredAtUtc,
            Action = "USER_PASSWORD_RESET",
            TargetObject = $"User:{userId:D}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserPasswordResetDataResult.Success();
    }
}
