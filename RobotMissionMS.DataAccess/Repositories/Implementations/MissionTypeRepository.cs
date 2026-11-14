using Microsoft.EntityFrameworkCore;
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
}
