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
                rt.Description,
                rt.SlotLengthCm,
                rt.SlotWidthCm,
                rt.SlotHeightCm,
                rt.MaxLoadPerSlotKg,
                rt.MaxPayloadKg,
                rt.SpeedMs,
                rt.HomeStationType))
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
                rt.Description,
                rt.SlotLengthCm,
                rt.SlotWidthCm,
                rt.SlotHeightCm,
                rt.MaxLoadPerSlotKg,
                rt.MaxPayloadKg,
                rt.SpeedMs,
                rt.HomeStationType))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        int slots,
        string colorHex,
        string? description,
        int? slotLengthCm,
        int? slotWidthCm,
        int? slotHeightCm,
        decimal? maxLoadPerSlotKg,
        decimal? maxPayloadKg,
        decimal? speedMs,
        string? homeStationType,
        CancellationToken cancellationToken = default)
    {
        var updated = await db.RobotTypes
            .Where(rt => rt.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rt => rt.Name, name)
                .SetProperty(rt => rt.Slots, slots)
                .SetProperty(rt => rt.ColorHex, colorHex)
                .SetProperty(rt => rt.Description, description)
                .SetProperty(rt => rt.SlotLengthCm, slotLengthCm)
                .SetProperty(rt => rt.SlotWidthCm, slotWidthCm)
                .SetProperty(rt => rt.SlotHeightCm, slotHeightCm)
                .SetProperty(rt => rt.MaxLoadPerSlotKg, maxLoadPerSlotKg)
                .SetProperty(rt => rt.MaxPayloadKg, maxPayloadKg)
                .SetProperty(rt => rt.SpeedMs, speedMs)
                .SetProperty(rt => rt.HomeStationType, homeStationType),
                cancellationToken);

        return updated > 0;
    }
}
