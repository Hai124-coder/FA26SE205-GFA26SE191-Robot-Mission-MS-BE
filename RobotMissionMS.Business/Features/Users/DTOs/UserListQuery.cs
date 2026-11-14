using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Users.DTOs;

public sealed class UserListQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(50)]
    public string? Role { get; init; }

    public bool? Active { get; init; }

    /// <summary>
    /// The first user-list endpoint supports the documented chronological order only.
    /// </summary>
    [StringLength(50)]
    public string? Sort { get; init; }
}
