using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.ChargingStations.DTOs;

public sealed class UpdateChargingStationRequest
{
    [Range(0, int.MaxValue)]
    public int Capacity { get; init; }
}
