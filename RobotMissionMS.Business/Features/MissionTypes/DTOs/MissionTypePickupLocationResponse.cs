namespace RobotMissionMS.Business.Features.MissionTypes.DTOs;

public sealed class MissionTypePickupLocationResponse
{
    public required string Floor { get; init; }
    public required string LocationCode { get; init; }
    public required string LocationName { get; init; }
}
