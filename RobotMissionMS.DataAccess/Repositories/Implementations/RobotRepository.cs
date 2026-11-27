using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
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

    public Task<string?> FindRobotFloorAsync(Guid robotId, CancellationToken cancellationToken = default) =>
        db.Robots.AsNoTracking().Where(robot => robot.Id == robotId)
            .Select(robot => (string?)robot.Zone.Code).SingleOrDefaultAsync(cancellationToken);

    public async Task<RobotHistoryData?> GetRobotHistoryAsync(
        Guid     robotId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Robots.AsNoTracking()
            .AnyAsync(r => r.Id == robotId, cancellationToken);
        if (!exists) return null;

        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.RobotId == robotId
                     && t.CreatedAt >= fromUtc
                     && t.CreatedAt <= toUtc)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new RobotHistoryTripRow(
                t.Id,
                t.TripCode,
                t.Status,
                t.Missions.Count,
                t.StartedAt,
                t.CompletedAt,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        var alerts = await db.Alerts.AsNoTracking()
            .Where(a => a.RobotId == robotId
                     && a.CreatedAt >= fromUtc
                     && a.CreatedAt <= toUtc)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new RobotHistoryAlertRow(
                a.Id,
                a.AlertType,
                a.Severity,
                a.Category,
                a.Status,
                a.Message,
                a.CreatedAt,
                a.ResolvedAt))
            .ToListAsync(cancellationToken);

        var chargingSessions = await db.ChargingSessions.AsNoTracking()
            .Where(cs => cs.RobotId == robotId
                      && (cs.StartedAt ?? cs.QueuedAt) >= fromUtc
                      && (cs.StartedAt ?? cs.QueuedAt) <= toUtc)
            .OrderByDescending(cs => cs.StartedAt ?? cs.QueuedAt)
            .Select(cs => new RobotHistoryChargingSessionRow(
                cs.Id,
                cs.ChargingStation.Name,
                cs.ChargingStation.Location.Code,
                cs.QueuedAt,
                cs.StartedAt,
                cs.EndedAt))
            .ToListAsync(cancellationToken);

        return new RobotHistoryData(trips, alerts, chargingSessions);
    }

    public Task<Guid?> FindRobotTypeIdByCodeAsync(string typeCode, CancellationToken cancellationToken = default) =>
        db.RobotTypes.AsNoTracking()
            .Where(rt => rt.TypeCode == typeCode)
            .Select(rt => (Guid?)rt.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindZoneIdByCodeAsync(string zoneCode, CancellationToken cancellationToken = default) =>
        db.Zones.AsNoTracking()
            .Where(z => z.Code == zoneCode && z.IsActive)
            .Select(z => (Guid?)z.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ChargingStationExistsAsync(Guid stationId, CancellationToken cancellationToken = default) =>
        db.ChargingStations.AsNoTracking()
            .AnyAsync(cs => cs.Id == stationId, cancellationToken);

    public async Task<CreateRobotRow?> UpdateRobotAsync(
        Guid  robotId,
        Guid? homeChargingStationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);
        if (robot is null) return null;
        robot.HomeChargingStationId = homeChargingStationId;
        AddActivity(actorUserId, "ROBOT_UPDATED", robotId);
        await db.SaveChangesAsync(cancellationToken);

        // Reload joined data for response
        var row = await (
            from r       in db.Robots.AsNoTracking()
            join zone    in db.Zones.AsNoTracking()      on r.ZoneId      equals zone.Id
            join robType in db.RobotTypes.AsNoTracking() on r.RobotTypeId equals robType.Id
            where r.Id == robotId
            select new { r, zone, robType })
            .SingleAsync(cancellationToken);

        string? homeStationCode = homeChargingStationId.HasValue
            ? await db.ChargingStations.AsNoTracking()
                .Where(cs => cs.Id == homeChargingStationId.Value)
                .Select(cs => (string?)cs.Location.Code)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new CreateRobotRow(
            row.r.Id,
            row.r.RobotCode,
            row.robType.TypeCode,
            row.robType.Name,
            row.zone.Code,
            row.zone.Name,
            RobotStateToString(row.r.State),
            row.r.IsConnected,
            row.r.Battery,
            homeStationCode,
            row.r.CreatedAt);
    }

    private static string RobotStateToString(RobotState state) => state switch
    {
        RobotState.Inactive    => "INACTIVE",
        RobotState.Available   => "AVAILABLE",
        RobotState.OnTrip      => "ON_TRIP",
        RobotState.Charging    => "CHARGING",
        RobotState.Error       => "ERROR",
        RobotState.Maintenance => "MAINTENANCE",
        _                      => state.ToString().ToUpperInvariant()
    };

    public async Task<SuspendRobotRepositoryResult> SuspendRobotAsync(
        Guid robotId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);

        if (robot is null)
            return SuspendRobotRepositoryResult.Failure(SuspendRobotRepositoryError.NotFound);

        // Already suspended (MAINTENANCE) or decommissioned (INACTIVE)
        if (robot.State == RobotState.Maintenance || robot.State == RobotState.Inactive)
            return SuspendRobotRepositoryResult.Failure(SuspendRobotRepositoryError.AlreadySuspended);

        // Set MAINTENANCE — robot finishes current trip then accepts no new ones
        robot.State = RobotState.Maintenance; AddActivity(actorUserId, "ROBOT_SUSPENDED", robotId); await db.SaveChangesAsync(cancellationToken);

        return SuspendRobotRepositoryResult.Success();
    }

    public async Task<BackToServiceRepositoryResult> BackToServiceAsync(
        Guid robotId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);

        if (robot is null)
            return BackToServiceRepositoryResult.Failure(BackToServiceRepositoryError.NotFound);

        if (robot.State != RobotState.Maintenance)
            return BackToServiceRepositoryResult.Failure(BackToServiceRepositoryError.NotInMaintenance);

        robot.State = RobotState.Available; AddActivity(actorUserId, "ROBOT_BACK_IN_SERVICE", robotId); await db.SaveChangesAsync(cancellationToken);

        return BackToServiceRepositoryResult.Success();
    }

    public async Task<MaintenanceRobotRepositoryResult> MaintenanceRobotAsync(
        Guid robotId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);

        if (robot is null)
            return MaintenanceRobotRepositoryResult.Failure(MaintenanceRobotRepositoryError.NotFound);

        if (robot.State == RobotState.Maintenance)
            return MaintenanceRobotRepositoryResult.Failure(MaintenanceRobotRepositoryError.AlreadyInMaintenance);

        var activeTripStatuses = new[] { TripStatus.Dispatched, TripStatus.InProgress };
        var onActiveTrip = await db.Trips.AsNoTracking()
            .AnyAsync(t => t.RobotId == robotId && activeTripStatuses.Contains(t.Status),
                cancellationToken);
        if (onActiveTrip)
            return MaintenanceRobotRepositoryResult.Failure(MaintenanceRobotRepositoryError.OnActiveTrip);

        robot.State = RobotState.Maintenance; AddActivity(actorUserId, "ROBOT_MAINTENANCE_ENTERED", robotId); await db.SaveChangesAsync(cancellationToken);

        return MaintenanceRobotRepositoryResult.Success();
    }

    public async Task<ChargeRobotRepositoryResult> ChargeRobotAsync(
        Guid robotId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);

        if (robot is null)
            return ChargeRobotRepositoryResult.Failure(ChargeRobotRepositoryError.NotFound);

        if (robot.State != RobotState.Available)
            return ChargeRobotRepositoryResult.Failure(ChargeRobotRepositoryError.NotAvailable);

        robot.State = RobotState.Charging; AddActivity(actorUserId, "ROBOT_CHARGE_REQUESTED", robotId); await db.SaveChangesAsync(cancellationToken);

        return ChargeRobotRepositoryResult.Success();
    }

    public async Task<ResumeRobotRepositoryResult> ResumeRobotAsync(
        Guid robotId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);

        if (robot is null)
            return ResumeRobotRepositoryResult.Failure(ResumeRobotRepositoryError.NotFound);

        // Only resume if currently suspended (MAINTENANCE)
        if (robot.State != RobotState.Maintenance)
            return ResumeRobotRepositoryResult.Failure(ResumeRobotRepositoryError.NotSuspended);

        robot.State = RobotState.Available; AddActivity(actorUserId, "ROBOT_RESUMED", robotId); await db.SaveChangesAsync(cancellationToken);

        return ResumeRobotRepositoryResult.Success();
    }

    public async Task<DeleteRobotRepositoryResult> DeleteRobotAsync(
        Guid robotId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var robot = await db.Robots.SingleOrDefaultAsync(r => r.Id == robotId, cancellationToken);
        if (robot is null)
            return DeleteRobotRepositoryResult.Failure(DeleteRobotRepositoryError.NotFound);

        var activeTripStatuses = new[] { TripStatus.Dispatched, TripStatus.InProgress };
        var onActiveTrip = await db.Trips.AsNoTracking()
            .AnyAsync(t => t.RobotId == robotId && activeTripStatuses.Contains(t.Status),
                cancellationToken);
        if (onActiveTrip)
            return DeleteRobotRepositoryResult.Failure(DeleteRobotRepositoryError.OnActiveTrip);

        robot.State = RobotState.Inactive; AddActivity(actorUserId, "ROBOT_DECOMMISSIONED", robotId); await db.SaveChangesAsync(cancellationToken);

        return DeleteRobotRepositoryResult.Success();
    }

    public async Task<CreateRobotRepositoryResult> CreateRobotAsync(
        string  robotCode,
        Guid    robotTypeId,
        Guid    zoneId,
        Guid?   homeChargingStationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        // Duplicate code check
        var duplicate = await db.Robots.AsNoTracking()
            .AnyAsync(r => r.RobotCode == robotCode, cancellationToken);
        if (duplicate)
            return CreateRobotRepositoryResult.Failure(CreateRobotRepositoryError.DuplicateCode);

        var robot = new Robot
        {
            Id                   = Guid.NewGuid(),
            RobotCode            = robotCode,
            RobotTypeId          = robotTypeId,
            ZoneId               = zoneId,
            HomeChargingStationId = homeChargingStationId,
            State                = RobotState.Inactive,
            IsConnected          = false,
            Battery              = 100,
            CreatedAt            = DateTime.UtcNow,
        };

        db.Robots.Add(robot);
        AddActivity(actorUserId, "ROBOT_CREATED", robot.Id);
        await db.SaveChangesAsync(cancellationToken);

        // Reload joined data for response
        var row = await (
            from r       in db.Robots.AsNoTracking()
            join zone    in db.Zones.AsNoTracking()      on r.ZoneId      equals zone.Id
            join robType in db.RobotTypes.AsNoTracking() on r.RobotTypeId equals robType.Id
            where r.Id == robot.Id
            select new { r, zone, robType })
            .SingleAsync(cancellationToken);

        string? homeStationCode = homeChargingStationId.HasValue
            ? await db.ChargingStations.AsNoTracking()
                .Where(cs => cs.Id == homeChargingStationId.Value)
                .Select(cs => (string?)cs.Location.Code)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return CreateRobotRepositoryResult.Success(new CreateRobotRow(
            row.r.Id,
            row.r.RobotCode,
            row.robType.TypeCode,
            row.robType.Name,
            row.zone.Code,
            row.zone.Name,
            "INACTIVE",
            false,
            100,
            homeStationCode,
            row.r.CreatedAt));
    }

    private void AddActivity(Guid actorUserId, string action, Guid robotId) => db.ActivityLogs.Add(new ActivityLog { UserId = actorUserId, Timestamp = DateTime.UtcNow, Action = action, TargetObject = $"Robot:{robotId:D}" });
}
