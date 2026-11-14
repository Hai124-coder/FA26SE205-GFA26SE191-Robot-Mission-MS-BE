using RobotMissionMS.Business.Features.Floors.DTOs;

namespace RobotMissionMS.Business.Features.Floors.Services;

public enum FloorMapError
{
    FloorNotFound,
    MapNotPublished
}

public sealed class FloorMapResult
{
    private FloorMapResult(FloorMapResponse? response, FloorMapError? error)
    {
        Response = response;
        Error = error;
    }

    public FloorMapResponse? Response { get; }
    public FloorMapError? Error { get; }
    public bool IsSuccess => Error is null;

    public static FloorMapResult Success(FloorMapResponse response) => new(response, null);
    public static FloorMapResult Failure(FloorMapError error) => new(null, error);
}
