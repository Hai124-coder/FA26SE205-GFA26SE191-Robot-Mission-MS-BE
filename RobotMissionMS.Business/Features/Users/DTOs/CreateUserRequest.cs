using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Users.DTOs;

public sealed class CreateUserRequest
{
    [Required]
    [StringLength(100)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string FullName { get; init; } = string.Empty;

    [EmailAddress]
    [StringLength(100)]
    public string? Email { get; init; }

    [Required]
    [StringLength(50)]
    public string Role { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Floor { get; init; }

}
