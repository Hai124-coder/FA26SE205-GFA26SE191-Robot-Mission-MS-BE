namespace RobotMissionMS.Business.Features.MissionTypes.DTOs;

public sealed class MissionTypeResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string RobotType { get; init; }
    public required string RobotTypeName { get; init; }
    public required string Mode { get; init; }
    public string? PickupKind { get; init; }
    public required bool UsesSlotSensor { get; init; }
    public required int DepartDelaySeconds { get; init; }
    public required int WaitTimeoutSeconds { get; init; }
    public required int BasePriority { get; init; }
}
