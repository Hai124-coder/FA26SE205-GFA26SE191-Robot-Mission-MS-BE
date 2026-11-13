using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Auth.DTOs;

public sealed class LoginRequest
{
    [Required]
    [StringLength(100)]
    public string Username { get; init; } = string.Empty;

    // Login accepts existing passwords; password creation rules belong elsewhere.
    [Required]
    [StringLength(1024)]
    public string Password { get; init; } = string.Empty;
}
