using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class ChargingStationRepository(RobotMissionDbContext db) : IChargingStationRepository
{
    public async Task<IReadOnlyList<ChargingStationRow>> GetChargingStationsAsync(CancellationToken cancellationToken = default)
    {
        var stations = await db.ChargingStations.AsNoTracking()
            .OrderBy(station => station.Location.Zone.Code).ThenBy(station => station.Location.Code)
            .Select(station => new { station.Id, station.Location.Code, station.Name, FloorCode = station.Location.Zone.Code,
                FloorName = station.Location.Zone.Name, station.StationType, station.Status, station.Capacity })
            .ToListAsync(cancellationToken);

        var stationIds = stations.Select(station => station.Id).ToList();
        var chargingRobots = await db.ChargingSessions.AsNoTracking()
            .Where(session => stationIds.Contains(session.ChargingStationId)
                && session.Status == ChargingSessionStatus.Charging)
            .OrderBy(session => session.StartedAt)
            .Select(session => new { session.ChargingStationId, Robot = new ChargingRobotRow(session.Robot.Id,
                session.Robot.RobotCode, session.Robot.Battery, session.StartedAt) })
            .ToListAsync(cancellationToken);

        var robotsByStation = chargingRobots.GroupBy(row => row.ChargingStationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ChargingRobotRow>)group.Select(row => row.Robot).ToList());
        return stations.Select(station => new ChargingStationRow(station.Id, station.Code, station.Name, station.FloorCode,
            station.FloorName, station.StationType, station.Status, station.Capacity,
            robotsByStation.GetValueOrDefault(station.Id, Array.Empty<ChargingRobotRow>()))).ToList();
    }
}
