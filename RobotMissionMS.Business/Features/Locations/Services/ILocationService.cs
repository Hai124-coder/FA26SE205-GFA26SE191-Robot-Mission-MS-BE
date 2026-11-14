using RobotMissionMS.Business.Features.Locations.DTOs;

namespace RobotMissionMS.Business.Features.Locations.Services;

public interface ILocationService
{
    Task<LocationListResult> GetLocationsAsync(LocationListQuery query, CancellationToken cancellationToken = default);
}
