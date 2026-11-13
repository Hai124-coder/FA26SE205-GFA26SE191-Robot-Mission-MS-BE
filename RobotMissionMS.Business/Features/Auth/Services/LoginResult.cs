using RobotMissionMS.Business.Features.Auth.DTOs;

namespace RobotMissionMS.Business.Features.Auth.Services;

public enum LoginError
{
    InvalidRequest,
    InvalidCredentials,
    AccountInactive
}

/// <summary>
/// Expected login outcomes, independent of HTTP. Infrastructure failures and
/// cancellation propagate to the caller. Never log the response's tokens.
/// </summary>
public sealed class LoginResult
{
    private LoginResult(AuthResponse? response, LoginError? error)
    {
        Response = response;
        Error = error;
    }

    public bool IsSuccess => Response is not null;
    public AuthResponse? Response { get; }
    public LoginError? Error { get; }

    internal static LoginResult Success(AuthResponse response) => new(response, null);
    internal static LoginResult Failure(LoginError error) => new(null, error);
}
