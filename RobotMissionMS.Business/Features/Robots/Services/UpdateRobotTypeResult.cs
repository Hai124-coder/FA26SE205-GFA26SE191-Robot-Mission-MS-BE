using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public enum UpdateRobotTypeError
{
    InvalidRequest,
    NotFound,
}

public sealed class UpdateRobotTypeResult
{
    public bool IsSuccess { get; private init; }
    public RobotTypeResponse? Response { get; private init; }
    public UpdateRobotTypeError? Error { get; private init; }

    public static UpdateRobotTypeResult Success(RobotTypeResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static UpdateRobotTypeResult Failure(UpdateRobotTypeError error) =>
        new() { IsSuccess = false, Error = error };
}
