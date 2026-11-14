using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IMapRepository
{
    Task<MapFloorRow?> FindFloorAsync(string floorCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapVersionRow>> GetVersionsAsync(Guid floorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> FindActiveLocationCodesWithoutNodesAsync(Guid floorId, IReadOnlyCollection<string> nodeCodes,
        CancellationToken cancellationToken = default);
    Task<MapVersionRow> CreateDraftAsync(Guid floorId, string sourceJson, string? backgroundImagePath, double widthM, double heightM,
        IReadOnlyList<MapNodeCreateRow> nodes, IReadOnlyList<MapEdgeCreateRow> edges, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
    Task<MapPublishData> PublishAsync(Guid floorId, Guid versionId, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
    Task<MapEdgeUpdateData> UpdateEdgeAsync(Guid floorId, Guid edgeId, bool isBlocked, string? reason, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FloorMaintenanceData?> UpdateMaintenanceAsync(Guid floorId, bool enabled, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record MapFloorRow(Guid Id, string Code, string Name);
public sealed record MapVersionRow(Guid Id, int VersionNo, MapVersionStatus Status, double WidthM, double HeightM,
    string? BackgroundImagePath, DateTime ImportedAt, DateTime? PublishedAt, int NodeCount, int EdgeCount);
public sealed record MapNodeCreateRow(string Code, NodeType NodeType, double X, double Y);
public sealed record MapEdgeCreateRow(string FromCode, string ToCode, double DistanceMeters, bool IsBidirectional);
public enum MapPublishDataKind { Success, VersionNotFound, VersionNotDraft, LocationsUseRemovedNodes }
public sealed record MapPublishData(MapPublishDataKind Kind, MapVersionRow? Version, IReadOnlyList<string> Locations);
public enum MapEdgeUpdateData { Success, EdgeNotFound, EdgeNotPublished }
public sealed record FloorMaintenanceData(string FloorCode, ZoneMode OperationMode, int ActiveTripCount);
