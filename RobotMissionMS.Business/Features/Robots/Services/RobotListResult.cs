using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public enum RobotListError
{
    InvalidQuery,
}

public sealed class RobotListResult
{
    public bool IsSuccess { get; private init; }
    public IReadOnlyList<RobotListItemResponse>? Response { get; private init; }
    public RobotListError? Error { get; private init; }

    public static RobotListResult Success(IReadOnlyList<RobotListItemResponse> response) =>
        new() { IsSuccess = true, Response = response };

    public static RobotListResult Failure(RobotListError error) =>
        new() { IsSuccess = false, Error = error };
}
