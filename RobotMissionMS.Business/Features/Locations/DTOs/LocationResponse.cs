namespace RobotMissionMS.Business.Features.Locations.DTOs;

public sealed class LocationResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Floor { get; init; }
    public required string FloorName { get; init; }
    public required string Kind { get; init; }
    public required string MapNodeCode { get; init; }
    public required bool IsRestrictedArea { get; init; }
    public string? RestrictedHours { get; init; }
    public required bool IsActive { get; init; }
}
