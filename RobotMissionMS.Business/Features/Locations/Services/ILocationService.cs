using RobotMissionMS.Business.Features.Locations.DTOs;

namespace RobotMissionMS.Business.Features.Locations.Services;

public interface ILocationService
{
    Task<LocationListResult> GetLocationsAsync(LocationListQuery query, CancellationToken cancellationToken = default);
    Task<CreateLocationResult> CreateLocationAsync(Guid createdByUserId, CreateLocationRequest request,
        CancellationToken cancellationToken = default);
    Task<UpdateLocationResult> UpdateLocationAsync(Guid updatedByUserId, string locationCode, UpdateLocationRequest request,
        CancellationToken cancellationToken = default);
    Task<UpdateLocationStatusResult> UpdateLocationStatusAsync(Guid updatedByUserId, string locationCode,
        UpdateLocationStatusRequest request, CancellationToken cancellationToken = default);
}
