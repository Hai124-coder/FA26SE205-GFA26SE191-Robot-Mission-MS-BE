namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

/// <summary>
/// Read-only data access for user administration. Returned rows contain no password
/// hash or refresh-token data.
/// </summary>
public interface IUserRepository
{
    Task<UserListPage> GetUsersAsync(
        int skip,
        int take,
        string? role,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<RoleRecord?> FindRoleByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<UserListRow?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the new account and its audit line atomically. The raw password is
    /// never accepted or persisted by this repository.
    /// </summary>
    Task<UserCreateDataResult> CreateUserAsync(
        UserCreateRecord user,
        Guid createdByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the permitted administrative fields and adds its audit entry in
    /// one transaction.
    /// </summary>
    Task<UserUpdateDataResult> UpdateUserAsync(
        Guid userId,
        string fullName,
        Guid roleId,
        Guid updatedByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes IsActive and writes an audit entry atomically. Deactivation also
    /// revokes every currently active refresh-token session for that user.
    /// </summary>
    Task<UserStatusUpdateDataResult> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        Guid updatedByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an already-hashed password, revokes active refresh-token sessions
    /// and writes an audit entry atomically.
    /// </summary>
    Task<UserPasswordResetDataResult> ResetUserPasswordAsync(
        Guid userId,
        string passwordHash,
        Guid resetByUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record UserListPage(IReadOnlyList<UserListRow> Items, int Total);

public sealed record UserListRow(
    Guid Id,
    string Username,
    string FullName,
    string? Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

public sealed record RoleRecord(Guid Id, string Name);

public sealed record UserCreateRecord(
    Guid Id,
    string Username,
    string PasswordHash,
    string FullName,
    string? Email,
    Guid RoleId,
    DateTime CreatedAt);

public enum UserCreateDataError
{
    UsernameAlreadyExists
}

public sealed class UserCreateDataResult
{
    private UserCreateDataResult(UserCreateDataError? error) => Error = error;

    public UserCreateDataError? Error { get; }

    public bool IsSuccess => Error is null;

    public static UserCreateDataResult Success() => new(null);

    public static UserCreateDataResult Failure(UserCreateDataError error) => new(error);
}

public enum UserUpdateDataError
{
    UserNotFound
}

public sealed class UserUpdateDataResult
{
    private UserUpdateDataResult(UserUpdateDataError? error) => Error = error;

    public UserUpdateDataError? Error { get; }

    public bool IsSuccess => Error is null;

    public static UserUpdateDataResult Success() => new(null);

    public static UserUpdateDataResult Failure(UserUpdateDataError error) => new(error);
}

public enum UserStatusUpdateDataError
{
    UserNotFound
}

public sealed class UserStatusUpdateDataResult
{
    private UserStatusUpdateDataResult(UserStatusUpdateDataError? error) => Error = error;

    public UserStatusUpdateDataError? Error { get; }

    public bool IsSuccess => Error is null;

    public static UserStatusUpdateDataResult Success() => new(null);

    public static UserStatusUpdateDataResult Failure(UserStatusUpdateDataError error) => new(error);
}

public enum UserPasswordResetDataError
{
    UserNotFound
}

public sealed class UserPasswordResetDataResult
{
    private UserPasswordResetDataResult(UserPasswordResetDataError? error) => Error = error;

    public UserPasswordResetDataError? Error { get; }

    public bool IsSuccess => Error is null;

    public static UserPasswordResetDataResult Success() => new(null);

    public static UserPasswordResetDataResult Failure(UserPasswordResetDataError error) => new(error);
}
