namespace RobotMissionMS.Business.Features.Robots.Services;

public enum ChargeRobotError
{
    NotFound,
    NotAvailable,
}

public sealed class ChargeRobotResult
{
    public bool IsSuccess { get; private init; }
    public ChargeRobotError? Error { get; private init; }

    public static ChargeRobotResult Success() => new() { IsSuccess = true };
    public static ChargeRobotResult Failure(ChargeRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
