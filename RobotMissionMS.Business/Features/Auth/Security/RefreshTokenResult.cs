namespace RobotMissionMS.Business.Features.Auth.Security;

/// <summary>
/// Return Token to the client; persist only TokenHash and the timestamps.
/// A class avoids exposing the token through a generated record ToString.
/// </summary>
public sealed class RefreshTokenResult(
    string token,
    string tokenHash,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt)
{
    public string Token { get; } = token;

    public string TokenHash { get; } = tokenHash;

    public DateTimeOffset CreatedAt { get; } = createdAt;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;
}
