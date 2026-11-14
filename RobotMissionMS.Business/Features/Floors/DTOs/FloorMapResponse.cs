namespace RobotMissionMS.Business.Features.Floors.DTOs;

/// <summary>The currently published map for one floor.</summary>
public sealed class FloorMapResponse
{
    public required string FloorCode { get; init; }
    public required string FloorName { get; init; }
    public required Guid VersionId { get; init; }
    public required int VersionNo { get; init; }
    public string? BackgroundImagePath { get; init; }
    public required double WidthM { get; init; }
    public required double HeightM { get; init; }
    public required IReadOnlyList<MapNodeResponse> Nodes { get; init; }
    public required IReadOnlyList<MapEdgeResponse> Edges { get; init; }
}

public sealed class MapNodeResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NodeType { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
}

public sealed class MapEdgeResponse
{
    public required Guid Id { get; init; }
    public required Guid FromNodeId { get; init; }
    public required Guid ToNodeId { get; init; }
    public required double DistanceMeters { get; init; }
    public required bool IsBidirectional { get; init; }
    public required bool IsBlocked { get; init; }
}
