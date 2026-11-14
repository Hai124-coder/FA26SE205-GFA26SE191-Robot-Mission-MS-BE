using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public enum RobotHistoryError { NotFound }

public sealed class RobotHistoryResult
{
    public bool IsSuccess { get; private init; }
    public RobotHistoryResponse? Response { get; private init; }
    public RobotHistoryError? Error { get; private init; }

    public static RobotHistoryResult Success(RobotHistoryResponse r) => new() { IsSuccess = true, Response = r };
    public static RobotHistoryResult Failure(RobotHistoryError e)    => new() { IsSuccess = false, Error = e };
}
