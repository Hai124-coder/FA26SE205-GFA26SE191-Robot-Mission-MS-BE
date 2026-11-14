using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Users.DTOs;

public sealed class UpdateUserRequest
{
    [Required]
    [StringLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Role { get; init; } = string.Empty;
}
