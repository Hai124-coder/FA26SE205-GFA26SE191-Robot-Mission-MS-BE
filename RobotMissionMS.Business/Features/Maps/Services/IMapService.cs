using RobotMissionMS.Business.Features.Maps.DTOs;

namespace RobotMissionMS.Business.Features.Maps.Services;

public interface IMapService
{
    Task<MapImportResult> ImportDraftAsync(Guid userId, string floorCode, MapImportRequest request,
        CancellationToken cancellationToken = default);
    Task<MapVersionListResult> GetVersionsAsync(string floorCode, CancellationToken cancellationToken = default);
    Task<PublishMapResult> PublishAsync(Guid userId, string floorCode, Guid versionId, CancellationToken cancellationToken = default);
    Task<UpdateMapEdgeResult> UpdateEdgeAsync(Guid userId, string floorCode, Guid edgeId, UpdateMapEdgeRequest request,
        CancellationToken cancellationToken = default);
    Task<FloorMaintenanceResult> UpdateMaintenanceAsync(Guid userId, string floorCode, UpdateFloorMaintenanceRequest request,
        CancellationToken cancellationToken = default);
}
