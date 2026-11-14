using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public enum RobotDetailError
{
    NotFound,
}

public sealed class RobotDetailResult
{
    public bool IsSuccess { get; private init; }
    public RobotDetailResponse? Response { get; private init; }
    public RobotDetailError? Error { get; private init; }

    public static RobotDetailResult Success(RobotDetailResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static RobotDetailResult Failure(RobotDetailError error) =>
        new() { IsSuccess = false, Error = error };
}
