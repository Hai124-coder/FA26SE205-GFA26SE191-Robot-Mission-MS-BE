using RobotMissionMS.Business.Features.Auth.DTOs;

namespace RobotMissionMS.Business.Features.Auth.Services;

public sealed class ChangePasswordResult
{
    private ChangePasswordResult(AuthResponse? response, ChangePasswordError? error) { Response = response; Error = error; }
    public AuthResponse? Response { get; }
    public ChangePasswordError? Error { get; }
    public bool IsSuccess => Error is null;
    public static ChangePasswordResult Success(AuthResponse response) => new(response, null);
    public static ChangePasswordResult Failure(ChangePasswordError error) => new(null, error);
}

public enum ChangePasswordError { InvalidRequest, WrongCurrentPassword, WeakPassword, PasswordSameAsOld, UserNotFound }
