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
