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

    public Task<RobotTypeRow?> FindByTypeCodeAsync(string typeCode, CancellationToken cancellationToken = default) =>
        db.RobotTypes
            .AsNoTracking()
            .Where(rt => rt.TypeCode == typeCode)
            .Select(rt => new RobotTypeRow(
                rt.Id,
                rt.TypeCode,
                rt.Name,
                rt.Slots,
                rt.ColorHex,
                rt.Description))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        int slots,
        string colorHex,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var updated = await db.RobotTypes
            .Where(rt => rt.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rt => rt.Name, name)
                .SetProperty(rt => rt.Slots, slots)
                .SetProperty(rt => rt.ColorHex, colorHex)
                .SetProperty(rt => rt.Description, description),
                cancellationToken);

        return updated > 0;
    }
}
