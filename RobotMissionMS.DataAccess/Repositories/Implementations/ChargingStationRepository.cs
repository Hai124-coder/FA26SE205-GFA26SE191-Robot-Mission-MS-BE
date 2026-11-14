using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class ChargingStationRepository(RobotMissionDbContext db) : IChargingStationRepository
{
    public async Task<IReadOnlyList<ChargingStationRow>> GetChargingStationsAsync(CancellationToken cancellationToken = default)
    {
        var stations = await db.ChargingStations.AsNoTracking()
            .OrderBy(station => station.Location.Zone.Code).ThenBy(station => station.Location.Code)
            .Select(station => new StationData(station.Id, station.Location.Code, station.Name, station.Location.Zone.Code,
                station.Location.Zone.Name, station.StationType, station.Status, station.Capacity))
            .ToListAsync(cancellationToken);

        return await ToRowsAsync(stations, cancellationToken);
    }

    public async Task<ChargingStationRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var station = await db.ChargingStations.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new StationData(item.Id, item.Location.Code, item.Name, item.Location.Zone.Code,
                item.Location.Zone.Name, item.StationType, item.Status, item.Capacity)).ToListAsync(cancellationToken);
        return station.Count == 0 ? null : (await ToRowsAsync(station, cancellationToken))[0];
    }

    public async Task<IReadOnlyList<ChargingStationLookupRow>> FindByLocationCodeAsync(string locationCode,
        CancellationToken cancellationToken = default) => await db.ChargingStations.AsNoTracking()
            .Where(station => station.Location.Code == locationCode).Select(station => new ChargingStationLookupRow(station.Id))
            .ToListAsync(cancellationToken);

    public async Task<ChargingStationCapacityUpdateResult> UpdateCapacityAsync(Guid id, int capacity, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var station = await db.ChargingStations.FromSqlInterpolated(
            $"SELECT * FROM public.\"ChargingStations\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var activeSessions = await db.ChargingSessions.CountAsync(session => session.ChargingStationId == id
            && session.Status == ChargingSessionStatus.Charging, cancellationToken);
        if (capacity < activeSessions)
            return ChargingStationCapacityUpdateResult.CapacityBelowActiveSessions;

        station.Capacity = capacity;
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), Action = "CHARGING_STATION_UPDATED",
            TargetObject = $"ChargingStation:{id:D};Capacity:{capacity}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ChargingStationCapacityUpdateResult.Success;
    }

    private async Task<IReadOnlyList<ChargingStationRow>> ToRowsAsync(IReadOnlyList<StationData> stations,
        CancellationToken cancellationToken)
    {
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
        return stations.Select(station => new ChargingStationRow(station.Id, station.LocationCode, station.Name, station.FloorCode,
            station.FloorName, station.StationType, station.Status, station.Capacity,
            robotsByStation.GetValueOrDefault(station.Id, Array.Empty<ChargingRobotRow>()))).ToList();
    }

    private sealed record StationData(Guid Id, string LocationCode, string Name, string FloorCode, string FloorName,
        string StationType, StationStatus Status, int Capacity);
}
