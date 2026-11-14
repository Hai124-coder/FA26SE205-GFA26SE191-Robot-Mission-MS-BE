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

    public async Task<RobotTypeRow?> FindByTypeCodeAsync(string typeCode, CancellationToken cancellationToken = default)
    {
        return await db.RobotTypes
            .AsNoTracking()
            .Where(rt => rt.TypeCode == typeCode)
            .Select(rt => new RobotTypeRow(
                rt.Id,
                rt.TypeCode,
                rt.Name,
                rt.Slots,
                rt.ColorHex,
                rt.Description))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(RobotTypeUpdateData data, CancellationToken cancellationToken = default)
    {
        var entity = await db.RobotTypes
            .FirstOrDefaultAsync(rt => rt.Id == data.Id, cancellationToken);

        if (entity is null)
            return false;

        entity.Name        = data.Name;
        entity.Slots       = data.Slots;
        entity.ColorHex    = data.ColorHex;
        entity.Description = data.Description;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
