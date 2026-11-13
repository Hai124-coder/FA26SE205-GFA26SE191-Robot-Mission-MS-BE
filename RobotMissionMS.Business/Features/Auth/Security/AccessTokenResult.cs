namespace RobotMissionMS.Business.Features.Auth.Security;

/// <summary>
/// An issued access token and its exact expiration time.
/// A class is used so the default ToString does not expose the token.
/// </summary>
public sealed class AccessTokenResult(string token, DateTimeOffset expiresAt)
{
    public string Token { get; } = token;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;
}
