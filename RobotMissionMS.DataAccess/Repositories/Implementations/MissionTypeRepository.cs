using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class MissionTypeRepository(RobotMissionDbContext db) : IMissionTypeRepository
{
    public async Task<IReadOnlyList<MissionTypeCatalogRow>> GetMissionTypesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.MissionTypes.AsNoTracking().OrderBy(type => type.Code)
            .Select(type => new MissionTypeCatalogRow(type.Id, type.Code, type.Name, type.RobotType.TypeCode,
                type.RobotType.Name, type.Mode, type.PickupKind, type.UsesSlotSensor, type.DepartDelaySec,
                type.WaitTimeoutSec, type.BasePriority, type.IsActive, type.RobotType.MaxLoadPerSlotKg)).ToListAsync(cancellationToken);
        return await AttachPickupLocationsAsync(rows, cancellationToken);
    }

    public Task<MissionTypeCatalogRow?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) => db.MissionTypes.AsNoTracking()
        .Where(type => type.Code == code).Select(type => new MissionTypeCatalogRow(type.Id, type.Code, type.Name,
            type.RobotType.TypeCode, type.RobotType.Name, type.Mode, type.PickupKind, type.UsesSlotSensor,
            type.DepartDelaySec, type.WaitTimeoutSec, type.BasePriority, type.IsActive, type.RobotType.MaxLoadPerSlotKg)).SingleOrDefaultAsync(cancellationToken);

    public async Task<MissionTypeCatalogRow> UpdateAsync(Guid id, int departDelaySec, int waitTimeoutSec, int basePriority,
        LocationKind? pickupKind, IReadOnlyList<MissionTypePickupLocationTarget>? pickupLocations, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var type = await db.MissionTypes.Include(value => value.RobotType).SingleAsync(value => value.Id == id, cancellationToken);
        type.DepartDelaySec = departDelaySec; type.WaitTimeoutSec = waitTimeoutSec; type.BasePriority = basePriority;
        type.PickupKind = pickupKind;
        if (pickupLocations is not null)
        {
            db.MissionTypePickupLocations.RemoveRange(db.MissionTypePickupLocations.Where(value => value.MissionTypeId == id));
            foreach (var pickup in pickupLocations) db.MissionTypePickupLocations.Add(new MissionTypePickupLocation
            {
                Id = Guid.NewGuid(), MissionTypeId = id, ZoneId = pickup.ZoneId, LocationId = pickup.LocationId,
                CreatedAt = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), UpdatedAt = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
                UpdatedByUserId = userId
            });
        }
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
            Action = "MISSION_TYPE_UPDATED", TargetObject = $"MissionType:{type.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        var row = new MissionTypeCatalogRow(type.Id, type.Code, type.Name, type.RobotType.TypeCode, type.RobotType.Name,
            type.Mode, type.PickupKind, type.UsesSlotSensor, type.DepartDelaySec, type.WaitTimeoutSec, type.BasePriority,
            type.IsActive, type.RobotType.MaxLoadPerSlotKg);
        return (await AttachPickupLocationsAsync([row], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<MissionTypePickupLocationTarget>?> ResolvePickupLocationsAsync(
        IReadOnlyList<MissionTypePickupLocationLookup> lookups, CancellationToken cancellationToken = default)
    {
        var floors = lookups.Select(value => value.FloorCode).Distinct().ToArray();
        var locations = lookups.Select(value => value.LocationCode).Distinct().ToArray();
        var candidates = await db.Locations.AsNoTracking().Where(value => value.IsActive && value.Zone.IsActive &&
                floors.Contains(value.Zone.Code) && locations.Contains(value.Code))
            .Select(value => new { FloorCode = value.Zone.Code, LocationCode = value.Code, value.ZoneId, value.Id })
            .ToListAsync(cancellationToken);
        var resolved = new List<MissionTypePickupLocationTarget>(lookups.Count);
        foreach (var lookup in lookups)
        {
            var candidate = candidates.SingleOrDefault(value => value.FloorCode == lookup.FloorCode && value.LocationCode == lookup.LocationCode);
            if (candidate is null) return null;
            resolved.Add(new MissionTypePickupLocationTarget(candidate.ZoneId, candidate.Id));
        }
        return resolved;
    }

    public async Task<bool> HasActiveWorkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await db.Missions.AnyAsync(value => value.MissionTypeId == id && (value.Status == MissionStatus.Pending ||
            value.Status == MissionStatus.Rescheduled || value.Status == MissionStatus.Assigned ||
            value.Status == MissionStatus.InProgress), cancellationToken)) return true;
        return await db.MissionSchedules.AnyAsync(value => value.MissionTypeId == id && value.IsActive, cancellationToken);
    }

    public async Task<MissionTypeCatalogRow> UpdateStatusAsync(Guid id, bool isActive, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var type = await db.MissionTypes.Include(value => value.RobotType).SingleAsync(value => value.Id == id, cancellationToken);
        type.IsActive = isActive;
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
            Action = "MISSION_TYPE_STATUS_UPDATED", TargetObject = $"MissionType:{type.Id:D};Active:{isActive}" });
        await db.SaveChangesAsync(cancellationToken);
        var row = new MissionTypeCatalogRow(type.Id, type.Code, type.Name, type.RobotType.TypeCode, type.RobotType.Name,
            type.Mode, type.PickupKind, type.UsesSlotSensor, type.DepartDelaySec, type.WaitTimeoutSec, type.BasePriority,
            type.IsActive, type.RobotType.MaxLoadPerSlotKg);
        return (await AttachPickupLocationsAsync([row], cancellationToken)).Single();
    }

    private async Task<IReadOnlyList<MissionTypeCatalogRow>> AttachPickupLocationsAsync(IReadOnlyList<MissionTypeCatalogRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(row => row.Id).ToArray();
        var mappings = await db.MissionTypePickupLocations.AsNoTracking()
            .Where(value => ids.Contains(value.MissionTypeId))
            .Select(value => new { value.MissionTypeId, FloorCode = value.Zone.Code,
                LocationCode = value.Location.Code, LocationName = value.Location.Name })
            .ToListAsync(cancellationToken);
        return rows.Select(row => row with
        {
            PickupLocations = mappings.Where(value => value.MissionTypeId == row.Id)
                .OrderBy(value => value.FloorCode).Select(value => new MissionTypePickupLocationRow(value.FloorCode,
                    value.LocationCode, value.LocationName)).ToList()
        }).ToList();
    }
}
