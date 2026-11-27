using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Auth.DTOs;

public sealed class ForgotPasswordRequest
{
    [Required, StringLength(100)]
    public string Username { get; init; } = string.Empty;

    [StringLength(120)]
    public string? Note { get; init; }
}
