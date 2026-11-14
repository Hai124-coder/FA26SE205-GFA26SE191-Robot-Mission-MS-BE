using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class ItemCatalogRepository(RobotMissionDbContext db) : IItemCatalogRepository
{
    public async Task<IReadOnlyList<ItemCatalogRow>> GetItemsAsync(string? missionTypeCode, bool? isActive,
        CancellationToken cancellationToken = default) => await db.Items.AsNoTracking()
            .Where(item => missionTypeCode == null || item.MissionType.Code == missionTypeCode)
            .Where(item => isActive == null || item.IsActive == isActive)
            .OrderBy(item => item.MissionType.Code).ThenBy(item => item.Code)
            .Select(item => new ItemCatalogRow(item.Id, item.Code, item.Name, item.Unit, item.MaxQtyPerSlot, item.IsActive,
                item.Description, item.MissionType.Code, item.MissionType.Name))
            .ToListAsync(cancellationToken);
}
