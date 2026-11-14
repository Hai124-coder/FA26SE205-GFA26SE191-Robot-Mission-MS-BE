using RobotMissionMS.Business.Features.ChargingStations.DTOs;

namespace RobotMissionMS.Business.Features.ChargingStations.Services;

public interface IChargingStationService
{
    Task<IReadOnlyList<ChargingStationResponse>> GetChargingStationsAsync(CancellationToken cancellationToken = default);
}
