using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Auth.DTOs;

public sealed class ChangePasswordRequest
{
    [Required, StringLength(1024)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(1024, MinimumLength = 8)]
    public string NewPassword { get; init; } = string.Empty;
}
