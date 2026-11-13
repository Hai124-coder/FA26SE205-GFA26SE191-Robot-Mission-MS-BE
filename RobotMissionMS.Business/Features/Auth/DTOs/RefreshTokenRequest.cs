using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Auth.DTOs;

/// <summary>
/// Shared request body for refresh and logout. Contains the original token,
/// not its database hash; token validity is checked by the future Auth service.
/// </summary>
public sealed class RefreshTokenRequest
{
    [Required]
    [StringLength(128)]
    public string RefreshToken { get; init; } = string.Empty;
}
