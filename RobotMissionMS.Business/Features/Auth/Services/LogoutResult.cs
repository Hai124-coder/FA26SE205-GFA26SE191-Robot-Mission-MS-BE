namespace RobotMissionMS.Business.Features.Auth.Services;

public enum LogoutError
{
    InvalidRequest
}

/// <summary>
/// Logout intentionally does not disclose whether a refresh token existed.
/// </summary>
public sealed class LogoutResult
{
    private LogoutResult(LogoutError? error) => Error = error;

    public bool IsSuccess => Error is null;
    public LogoutError? Error { get; }

    internal static LogoutResult Success() => new(null);
    internal static LogoutResult Failure(LogoutError error) => new(error);
}
