using RobotMissionMS.Business.Features.ChargingStations.DTOs;

namespace RobotMissionMS.Business.Features.ChargingStations.Services;

public interface IChargingStationService
{
    Task<ChargingStationListResult> GetChargingStationsAsync(ChargingStationListQuery query, string? operatorFloor,
        CancellationToken cancellationToken = default);
    Task<UpdateChargingStationResult> UpdateChargingStationAsync(Guid updatedByUserId, string locationCode,
        UpdateChargingStationRequest request, CancellationToken cancellationToken = default);
}
