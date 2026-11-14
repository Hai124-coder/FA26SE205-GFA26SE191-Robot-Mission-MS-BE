using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class RobotRepository(RobotMissionDbContext db) : IRobotRepository
{
    public async Task<IReadOnlyList<RobotListRow>> GetRobotsAsync(
        string?     floorCode,
        string?     typeCode,
        RobotState? state,
        bool?       isConnected,
        CancellationToken cancellationToken = default)
    {
        // Active trip = DISPATCHED or IN_PROGRESS
        var activeTripStatuses = new[] { TripStatus.Dispatched, TripStatus.InProgress };

        var query =
            from robot in db.Robots.AsNoTracking()
            join zone     in db.Zones.AsNoTracking()      on robot.ZoneId      equals zone.Id
            join robType  in db.RobotTypes.AsNoTracking() on robot.RobotTypeId equals robType.Id
            select new
            {
                robot.Id,
                robot.RobotCode,
                TypeCode    = robType.TypeCode,
                TypeName    = robType.Name,
                FloorCode   = zone.Code,
                FloorName   = zone.Name,
                robot.State,
                robot.IsConnected,
                robot.Battery,
                robot.CurrentX,
                robot.CurrentY,
                robot.LastHeartbeatAt,
                ActiveTripCode = db.Trips.AsNoTracking()
                    .Where(t => t.RobotId == robot.Id && activeTripStatuses.Contains(t.Status))
                    .Select(t => (string?)t.TripCode)
                    .FirstOrDefault()
            };

        if (!string.IsNullOrWhiteSpace(floorCode))
            query = query.Where(r => r.FloorCode == floorCode);

        if (!string.IsNullOrWhiteSpace(typeCode))
            query = query.Where(r => r.TypeCode == typeCode);

        if (state.HasValue)
            query = query.Where(r => r.State == state.Value);

        if (isConnected.HasValue)
            query = query.Where(r => r.IsConnected == isConnected.Value);

        var rows = await query
            .OrderBy(r => r.RobotCode)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RobotListRow(
            r.Id,
            r.RobotCode,
            r.TypeCode,
            r.TypeName,
            r.FloorCode,
            r.FloorName,
            r.State,
            r.IsConnected,
            r.Battery,
            r.CurrentX,
            r.CurrentY,
            r.ActiveTripCode,
            r.LastHeartbeatAt
        )).ToList();
    }
}
