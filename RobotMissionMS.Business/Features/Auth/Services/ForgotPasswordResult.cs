namespace RobotMissionMS.Business.Features.Auth.Services;

public sealed class ForgotPasswordResult
{
    private ForgotPasswordResult(bool invalidRequest) => InvalidRequest = invalidRequest;
    public bool InvalidRequest { get; }
    public static ForgotPasswordResult Accepted() => new(false);
    public static ForgotPasswordResult Invalid() => new(true);
}
