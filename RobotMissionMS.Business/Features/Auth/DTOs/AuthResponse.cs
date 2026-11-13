namespace RobotMissionMS.Business.Features.Auth.DTOs;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset RefreshTokenExpiresAt { get; init; }

    public required AuthUserResponse User { get; init; }
}