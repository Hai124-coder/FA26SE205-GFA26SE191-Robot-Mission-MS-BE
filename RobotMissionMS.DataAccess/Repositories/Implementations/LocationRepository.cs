using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class LocationRepository(RobotMissionDbContext db) : ILocationRepository
{
    public async Task<IReadOnlyList<LocationRow>> GetLocationsAsync(string? floorCode, LocationKind? kind,
        CancellationToken cancellationToken = default)
    {
        var query = db.Locations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(floorCode))
            query = query.Where(location => location.Zone.Code == floorCode);
        if (kind.HasValue)
            query = query.Where(location => location.Kind == kind.Value);

        return await query.OrderBy(location => location.Zone.Code).ThenBy(location => location.Code)
            .Select(location => new LocationRow(location.Id, location.Code, location.Name, location.Description,
                location.Zone.Code, location.Zone.Name, location.Kind, location.MapNodeCode, location.IsRestrictedArea,
                location.RestrictedHours, location.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocationManagementRow>> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await db.Locations.AsNoTracking().Where(location => location.Code == code).OrderBy(location => location.Zone.Code)
            .Select(location => new LocationManagementRow(location.Id, location.Code, location.Name, location.Description,
                location.ZoneId, location.Zone.Code, location.Zone.Name, location.Kind, location.MapNodeCode,
                location.IsRestrictedArea, location.RestrictedHours, location.IsActive)).ToListAsync(cancellationToken);

    public Task<FloorLookupRow?> FindFloorAsync(string code, CancellationToken cancellationToken = default) => db.Zones.AsNoTracking()
        .Where(zone => zone.Code == code).Select(zone => new FloorLookupRow(zone.Id, zone.Code, zone.Name, zone.IsActive))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> PublishedMapContainsNodeAsync(Guid floorId, string nodeCode, CancellationToken cancellationToken = default) =>
        db.MapNodes.AsNoTracking().AnyAsync(node => node.Code == nodeCode
            && node.MapVersion.ZoneId == floorId && node.MapVersion.Status == MapVersionStatus.Published, cancellationToken);

    public async Task<LocationManagementRow> CreateLocationAsync(string code, string name, string? description, Guid floorId,
        string floorCode, string floorName, LocationKind kind, string mapNodeCode, bool isRestrictedArea, string? restrictedHours,
        Guid userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var location = new Location { Id = Guid.NewGuid(), Code = code, Name = name, Description = description, ZoneId = floorId,
            Kind = kind, MapNodeCode = mapNodeCode, IsRestrictedArea = isRestrictedArea, RestrictedHours = restrictedHours, IsActive = true };
        db.Locations.Add(location);
        AddAuditLog(userId, occurredAtUtc, "LOCATION_CREATED", $"Location:{code}");
        await db.SaveChangesAsync(cancellationToken);
        return new LocationManagementRow(location.Id, location.Code, location.Name, location.Description, floorId, floorCode, floorName,
            location.Kind, location.MapNodeCode, location.IsRestrictedArea, location.RestrictedHours, location.IsActive);
    }

    public async Task<LocationManagementRow> UpdateLocationAsync(Guid locationId, string name, string? description, LocationKind kind,
        string mapNodeCode, bool isRestrictedArea, string? restrictedHours, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var location = await db.Locations.Include(item => item.Zone).SingleAsync(item => item.Id == locationId, cancellationToken);
        location.Name = name; location.Description = description; location.Kind = kind; location.MapNodeCode = mapNodeCode;
        location.IsRestrictedArea = isRestrictedArea; location.RestrictedHours = restrictedHours;
        AddAuditLog(userId, occurredAtUtc, "LOCATION_UPDATED", $"Location:{location.Code}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToManagementRow(location);
    }

    public async Task<LocationManagementRow> UpdateLocationStatusAsync(Guid locationId, bool isActive, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var location = await db.Locations.Include(item => item.Zone).SingleAsync(item => item.Id == locationId, cancellationToken);
        location.IsActive = isActive;
        AddAuditLog(userId, occurredAtUtc, "LOCATION_STATUS_CHANGED", $"Location:{location.Code};Active:{isActive}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToManagementRow(location);
    }

    public async Task<LocationUsageRow> GetUsageAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var pending = await db.Missions.AsNoTracking().Where(mission => (mission.FromLocationId == locationId || mission.ToLocationId == locationId)
            && (mission.Status == MissionStatus.Pending || mission.Status == MissionStatus.Rescheduled))
            .OrderBy(mission => mission.MissionCode).Select(mission => mission.MissionCode).ToListAsync(cancellationToken);
        var schedules = await db.MissionScheduleStops.AsNoTracking().Where(stop => stop.LocationId == locationId && stop.Schedule.IsActive)
            .OrderBy(stop => stop.Schedule.Name).Select(stop => stop.Schedule.Name).Distinct().ToListAsync(cancellationToken);
        var activeSessions = await db.ChargingSessions.AsNoTracking().CountAsync(session => session.ChargingStation.LocationId == locationId
            && session.Status == ChargingSessionStatus.Charging, cancellationToken);
        return new LocationUsageRow(pending, schedules, activeSessions);
    }

    private static LocationManagementRow ToManagementRow(Location location) => new(location.Id, location.Code, location.Name,
        location.Description, location.ZoneId, location.Zone.Code, location.Zone.Name, location.Kind, location.MapNodeCode,
        location.IsRestrictedArea, location.RestrictedHours, location.IsActive);

    private void AddAuditLog(Guid userId, DateTime occurredAtUtc, string action, string targetObject) => db.ActivityLogs.Add(new ActivityLog
    {
        UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), Action = action, TargetObject = targetObject
    });
}
