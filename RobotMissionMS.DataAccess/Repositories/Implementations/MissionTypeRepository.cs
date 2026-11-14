using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class MissionTypeRepository(RobotMissionDbContext db) : IMissionTypeRepository
{
    public async Task<IReadOnlyList<MissionTypeCatalogRow>> GetMissionTypesAsync(CancellationToken cancellationToken = default) =>
        await db.MissionTypes.AsNoTracking().OrderBy(type => type.Code)
            .Select(type => new MissionTypeCatalogRow(type.Id, type.Code, type.Name, type.RobotType.TypeCode,
                type.RobotType.Name, type.Mode, type.PickupKind, type.UsesSlotSensor, type.DepartDelaySec,
                type.WaitTimeoutSec, type.BasePriority)).ToListAsync(cancellationToken);

    public Task<MissionTypeCatalogRow?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) => db.MissionTypes.AsNoTracking()
        .Where(type => type.Code == code).Select(type => new MissionTypeCatalogRow(type.Id, type.Code, type.Name,
            type.RobotType.TypeCode, type.RobotType.Name, type.Mode, type.PickupKind, type.UsesSlotSensor,
            type.DepartDelaySec, type.WaitTimeoutSec, type.BasePriority)).SingleOrDefaultAsync(cancellationToken);

    public async Task<MissionTypeCatalogRow> UpdateAsync(Guid id, int departDelaySec, int waitTimeoutSec, int basePriority,
        LocationKind? pickupKind, Guid userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var type = await db.MissionTypes.Include(value => value.RobotType).SingleAsync(value => value.Id == id, cancellationToken);
        type.DepartDelaySec = departDelaySec; type.WaitTimeoutSec = waitTimeoutSec; type.BasePriority = basePriority;
        type.PickupKind = pickupKind;
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
            Action = "MISSION_TYPE_UPDATED", TargetObject = $"MissionType:{type.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        return new MissionTypeCatalogRow(type.Id, type.Code, type.Name, type.RobotType.TypeCode, type.RobotType.Name,
            type.Mode, type.PickupKind, type.UsesSlotSensor, type.DepartDelaySec, type.WaitTimeoutSec, type.BasePriority);
    }
}
