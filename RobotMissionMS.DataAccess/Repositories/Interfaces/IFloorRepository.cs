using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IFloorRepository
{
    Task<IReadOnlyList<FloorRow>> GetFloorsAsync(CancellationToken cancellationToken = default);
    Task<FloorRow?> FindFloorByCodeAsync(string floorCode, CancellationToken cancellationToken = default);
    Task<PublishedMapRow?> GetPublishedMapAsync(Guid zoneId, CancellationToken cancellationToken = default);
}

public sealed record FloorRow(Guid Id, string Code, string Name, string? Description, bool IsActive, ZoneMode OperationMode);
public sealed record PublishedMapRow(Guid Id, int VersionNo, string? BackgroundImagePath, double WidthM, double HeightM,
    IReadOnlyList<MapNodeRow> Nodes, IReadOnlyList<MapEdgeRow> Edges);
public sealed record MapNodeRow(Guid Id, string Code, NodeType NodeType, double X, double Y);
public sealed record MapEdgeRow(Guid Id, Guid FromNodeId, Guid ToNodeId, double DistanceMeters, bool IsBidirectional, bool IsBlocked);
