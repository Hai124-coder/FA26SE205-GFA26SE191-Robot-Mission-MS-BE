using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Auth.Configuration;

/// <summary>
/// Settings from the Auth section. This class does not create sessions or tokens.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Absolute session lifetime starting at login, not extended by refresh.
    /// Default: 7 days. Configuration accepts 1 to 365 days.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;
}
