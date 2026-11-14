namespace RobotMissionMS.Business.Features.Items.DTOs;

public sealed class ItemResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string MissionType { get; init; }
    public required string MissionTypeName { get; init; }
    public required string Unit { get; init; }
    public required int MaxQtyPerSlot { get; init; }
    public string? Description { get; init; }
    public string? Packaging { get; init; }
    public decimal? KgPerUnit { get; init; }
    public string? CapacityLimitReason { get; init; }
    public decimal? MaxLoadPerSlotKg { get; init; }
    public required bool MissionTypeIsActive { get; init; }
    public required bool IsActive { get; init; }
}
