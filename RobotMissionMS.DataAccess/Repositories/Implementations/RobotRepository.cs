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

    public async Task<RobotDetailRow?> GetRobotByIdAsync(Guid robotId, CancellationToken cancellationToken = default)
    {
        var activeTripStatuses = new[] { TripStatus.Dispatched, TripStatus.InProgress };
        var openAlertStatus    = AlertStatus.Open;

        // Load robot + type + zone in one query
        var robot = await (
            from r       in db.Robots.AsNoTracking()
            join zone    in db.Zones.AsNoTracking()      on r.ZoneId      equals zone.Id
            join robType in db.RobotTypes.AsNoTracking() on r.RobotTypeId equals robType.Id
            where r.Id == robotId
            select new
            {
                r.Id,
                r.RobotCode,
                TypeCode        = robType.TypeCode,
                TypeName        = robType.Name,
                TypeSlots       = robType.Slots,
                FloorCode       = zone.Code,
                FloorName       = zone.Name,
                r.State,
                r.IsConnected,
                r.Battery,
                r.CurrentX,
                r.CurrentY,
                r.LastHeartbeatAt,
                r.HomeChargingStationId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (robot is null) return null;

        // Home station code
        string? homeStationCode = null;
        if (robot.HomeChargingStationId.HasValue)
        {
            homeStationCode = await db.ChargingStations.AsNoTracking()
                .Where(cs => cs.Id == robot.HomeChargingStationId.Value)
                .Select(cs => (string?)cs.Location.Code)
                .SingleOrDefaultAsync(cancellationToken);
        }

        // Active missions in slots (IN_PROGRESS or ASSIGNED on this robot's current trip)
        var missionSlots = await (
            from m    in db.Missions.AsNoTracking()
            join item in db.Items.AsNoTracking()     on m.ItemId        equals item.Id     into itemJoin
            from item in itemJoin.DefaultIfEmpty()
            join toLoc in db.Locations.AsNoTracking() on m.ToLocationId equals toLoc.Id   into toLocJoin
            from toLoc in toLocJoin.DefaultIfEmpty()
            where m.SlotNo != null
                && (m.Status == MissionStatus.Assigned || m.Status == MissionStatus.InProgress)
                && m.TripId != null
                && m.Trip!.RobotId == robotId
                && activeTripStatuses.Contains(m.Trip!.Status)
            orderby m.SlotNo
            select new RobotMissionSlotRow(
                m.SlotNo!.Value,
                m.MissionCode,
                item != null ? item.Code   : null,
                item != null ? item.Name   : null,
                m.Quantity,
                toLoc != null ? toLoc.Code : null,
                toLoc != null ? toLoc.Name : null,
                m.Status == MissionStatus.Assigned   ? "ASSIGNED"    :
                m.Status == MissionStatus.InProgress ? "IN_PROGRESS" : m.Status.ToString().ToUpperInvariant()
            ))
            .ToListAsync(cancellationToken);

        // Active trip with stops
        RobotActiveTripRow? activeTrip = null;
        var trip = await db.Trips.AsNoTracking()
            .Where(t => t.RobotId == robotId && activeTripStatuses.Contains(t.Status))
            .Select(t => new { t.Id, t.TripCode, t.Status, t.StartedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is not null)
        {
            var stops = await (
                from ts   in db.TripStops.AsNoTracking()
                join loc  in db.Locations.AsNoTracking() on ts.LocationId equals loc.Id
                where ts.TripId == trip.Id
                orderby ts.Seq
                select new RobotTripStopRow(ts.Seq, loc.Code, loc.Name, ts.ArrivedAt, ts.DepartedAt))
                .ToListAsync(cancellationToken);

            activeTrip = new RobotActiveTripRow(
                trip.Id,
                trip.TripCode,
                trip.Status,
                trip.StartedAt,
                stops);
        }

        // Open alerts
        var openAlerts = await db.Alerts.AsNoTracking()
            .Where(a => a.RobotId == robotId && a.Status == openAlertStatus)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new RobotAlertRow(a.Id, a.AlertType, a.Severity, a.Category, a.Message, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new RobotDetailRow(
            robot.Id,
            robot.RobotCode,
            robot.TypeCode,
            robot.TypeName,
            robot.TypeSlots,
            robot.FloorCode,
            robot.FloorName,
            robot.State,
            robot.IsConnected,
            robot.Battery,
            robot.CurrentX,
            robot.CurrentY,
            robot.LastHeartbeatAt,
            homeStationCode,
            missionSlots,
            activeTrip,
            openAlerts);
    }
}
