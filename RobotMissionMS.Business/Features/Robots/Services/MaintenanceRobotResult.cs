namespace RobotMissionMS.Business.Features.Robots.Services;

public enum MaintenanceRobotError
{
    NotFound,
    OnActiveTrip,
    AlreadyInMaintenance,
}

public sealed class MaintenanceRobotResult
{
    public bool IsSuccess { get; private init; }
    public MaintenanceRobotError? Error { get; private init; }

    public static MaintenanceRobotResult Success() => new() { IsSuccess = true };
    public static MaintenanceRobotResult Failure(MaintenanceRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
