namespace RobotMissionMS.Business.Features.Maps.DTOs;

public sealed class MapVersionResponse
{
    public required Guid Id { get; init; }
    public required int VersionNo { get; init; }
    public required string Status { get; init; }
    public required double WidthM { get; init; }
    public required double HeightM { get; init; }
    public string? BackgroundImagePath { get; init; }
    public required DateTime ImportedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required int NodeCount { get; init; }
    public required int EdgeCount { get; init; }
}

public sealed class MapImportResponse
{
    public required MapVersionResponse MapVersion { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<string> AffectedLocationCodes { get; init; }
}
