namespace RobotMissionMS.Business.Features.Maps.DTOs;

public sealed class UpdateFloorMaintenanceRequest
{
    public bool Enabled { get; init; }
}

public sealed class FloorMaintenanceResponse
{
    public required string Floor { get; init; }
    public required string OperationMode { get; init; }
    public required int ActiveTripCount { get; init; }
}
