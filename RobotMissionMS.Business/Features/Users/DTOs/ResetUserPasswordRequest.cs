using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Users.DTOs;

public sealed class ResetUserPasswordRequest
{
    [Required]
    [StringLength(1024, MinimumLength = 8)]
    public string TemporaryPassword { get; init; } = string.Empty;
}
