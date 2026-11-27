using RobotMissionMS.Business.Features.Locations.DTOs;
using RobotMissionMS.Business.Common.DTOs;

namespace RobotMissionMS.Business.Features.Locations.Services;

public enum LocationListError
{
    InvalidQuery
}

public sealed class LocationListResult
{
    private LocationListResult(PagedResponse<LocationResponse>? response, LocationListError? error)
    {
        Response = response;
        Error = error;
    }

    public PagedResponse<LocationResponse>? Response { get; }
    public LocationListError? Error { get; }
    public bool IsSuccess => Error is null;

    public static LocationListResult Success(PagedResponse<LocationResponse> response) => new(response, null);
    public static LocationListResult Failure(LocationListError error) => new(null, error);
}
