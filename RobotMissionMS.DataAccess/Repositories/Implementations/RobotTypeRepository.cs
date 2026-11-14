using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class RobotTypeRepository(RobotMissionDbContext db) : IRobotTypeRepository
{
    public async Task<IReadOnlyList<RobotTypeRow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.RobotTypes
            .AsNoTracking()
            .OrderBy(rt => rt.TypeCode)
            .Select(rt => new RobotTypeRow(
                rt.Id,
                rt.TypeCode,
                rt.Name,
                rt.Slots,
                rt.ColorHex,
                rt.Description))
            .ToListAsync(cancellationToken);
    }
}
