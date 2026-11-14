using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public sealed class UpdateRobotTypeResult
{
    private UpdateRobotTypeResult(RobotTypeResponse? response, UpdateRobotTypeError? error)
    {
        Response = response;
        Error = error;
    }

    public RobotTypeResponse? Response { get; }
    public UpdateRobotTypeError? Error { get; }
    public bool IsSuccess => Response is not null;

    public static UpdateRobotTypeResult Success(RobotTypeResponse response) => new(response, null);
    public static UpdateRobotTypeResult Failure(UpdateRobotTypeError error) => new(null, error);
}

public enum UpdateRobotTypeError
{
    InvalidRequest,
    RobotTypeNotFound
}
