using RobotMissionMS.Business.Features.Auth.DTOs;

namespace RobotMissionMS.Business.Features.Auth.Services;

public enum RefreshError
{
    InvalidRequest,
    InvalidRefreshToken,
    AccountInactive
}

/// <summary>
/// Expected refresh outcomes, independent of HTTP. The response contains raw
/// tokens only on success and must never be logged.
/// </summary>
public sealed class RefreshResult
{
    private RefreshResult(AuthResponse? response, RefreshError? error)
    {
        Response = response;
        Error = error;
    }

    public bool IsSuccess => Response is not null;
    public AuthResponse? Response { get; }
    public RefreshError? Error { get; }

    internal static RefreshResult Success(AuthResponse response) => new(response, null);
    internal static RefreshResult Failure(RefreshError error) => new(null, error);
}
