using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public enum CreateRobotError
{
    InvalidRequest,
    RobotTypeNotFound,
    ZoneNotFound,
    HomeStationNotFound,
    DuplicateCode,
}

public sealed class CreateRobotResult
{
    public bool IsSuccess { get; private init; }
    public CreateRobotResponse? Response { get; private init; }
    public CreateRobotError? Error { get; private init; }

    public static CreateRobotResult Success(CreateRobotResponse r) =>
        new() { IsSuccess = true, Response = r };
    public static CreateRobotResult Failure(CreateRobotError e) =>
        new() { IsSuccess = false, Error = e };
}
