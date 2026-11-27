namespace RobotMissionMS.Business.Features.Users.DTOs;

public sealed class UserListItemResponse
{
    public required Guid Id { get; init; }

    public required string Username { get; init; }

    public required string FullName { get; init; }

    public string? Email { get; init; }

    public required string Role { get; init; }

    public string? Floor { get; init; }

    public required bool IsActive { get; init; }

    public required bool MustChangePassword { get; init; }

    public required bool HasOpenResetRequest { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>Returned only by POST /users and POST /users/{id}/reset-password.</summary>
    public string? TempPassword { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
