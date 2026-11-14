using RobotMissionMS.Business.Features.Floors.DTOs;

namespace RobotMissionMS.Business.Features.Floors.Services;

public interface IFloorService
{
    Task<IReadOnlyList<FloorResponse>> GetFloorsAsync(CancellationToken cancellationToken = default);
    Task<FloorMapResult> GetPublishedMapAsync(string floorCode, CancellationToken cancellationToken = default);
    Task<CreateFloorResult> CreateFloorAsync(Guid createdByUserId, CreateFloorRequest request,
        CancellationToken cancellationToken = default);
    Task<UpdateFloorResult> UpdateFloorAsync(Guid updatedByUserId, string floorCode, UpdateFloorRequest request,
        CancellationToken cancellationToken = default);
}
