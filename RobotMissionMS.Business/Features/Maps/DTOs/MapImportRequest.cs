using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Maps.DTOs;

/// <summary>JSON payload accepted in the map-package file uploaded to map-versions.</summary>
public sealed class MapImportRequest
{
    [Range(0.01, 100000)] public double WidthM { get; init; }
    [Range(0.01, 100000)] public double HeightM { get; init; }
    [StringLength(500)] public string? BackgroundImagePath { get; init; }
    [Required] public IReadOnlyList<MapNodeInput> Nodes { get; init; } = Array.Empty<MapNodeInput>();
    [Required] public IReadOnlyList<MapEdgeInput> Edges { get; init; } = Array.Empty<MapEdgeInput>();
}

public sealed class MapNodeInput
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; init; } = string.Empty;
    [Required, StringLength(20, MinimumLength = 1)] public string NodeType { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
}

public sealed class MapEdgeInput
{
    [Required, StringLength(50, MinimumLength = 1)] public string From { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string To { get; init; } = string.Empty;
    [Range(0.001, 100000)] public double DistanceMeters { get; init; }
    public bool IsBidirectional { get; init; } = true;
}
