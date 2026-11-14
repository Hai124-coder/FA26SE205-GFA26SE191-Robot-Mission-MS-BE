using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public enum UpdateRobotError
{
    NotFound,
    HomeStationNotFound,
}

public sealed class UpdateRobotResult
{
    public bool IsSuccess { get; private init; }
    public CreateRobotResponse? Response { get; private init; }
    public UpdateRobotError? Error { get; private init; }

    public static UpdateRobotResult Success(CreateRobotResponse r) =>
        new() { IsSuccess = true, Response = r };
    public static UpdateRobotResult Failure(UpdateRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
