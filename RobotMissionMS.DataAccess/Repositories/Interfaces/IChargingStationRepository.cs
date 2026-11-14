using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IChargingStationRepository
{
    Task<IReadOnlyList<ChargingStationRow>> GetChargingStationsAsync(CancellationToken cancellationToken = default);
}

public sealed record ChargingStationRow(Guid Id, string LocationCode, string Name, string FloorCode, string FloorName,
    string StationType, StationStatus Status, int Capacity, IReadOnlyList<ChargingRobotRow> ChargingRobots);
public sealed record ChargingRobotRow(Guid Id, string RobotCode, int Battery, DateTime StartedAt);
