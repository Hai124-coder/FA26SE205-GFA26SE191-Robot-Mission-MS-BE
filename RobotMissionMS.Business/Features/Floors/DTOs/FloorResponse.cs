namespace RobotMissionMS.Business.Features.Floors.DTOs;

/// <summary>One hospital floor. In the database, a floor is stored as a Zone.</summary>
public sealed class FloorResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required bool IsActive { get; init; }
    public required string OperationMode { get; init; }
}
