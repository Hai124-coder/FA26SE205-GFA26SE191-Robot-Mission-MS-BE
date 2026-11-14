using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
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
                item.Description, item.MissionTypeId, item.MissionType.Code, item.MissionType.Name))
            .ToListAsync(cancellationToken);

    public Task<ItemCatalogRow?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) => db.Items.AsNoTracking()
        .Where(item => item.Code == code).Select(item => new ItemCatalogRow(item.Id, item.Code, item.Name, item.Unit,
            item.MaxQtyPerSlot, item.IsActive, item.Description, item.MissionTypeId, item.MissionType.Code,
            item.MissionType.Name)).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasMissionsAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        db.Missions.AnyAsync(mission => mission.ItemId == itemId, cancellationToken);

    public async Task<ItemCatalogRow> CreateAsync(string code, string name, string unit, Guid missionTypeId, string missionTypeCode,
        string missionTypeName, int maxQtyPerSlot, string? description, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var item = new Item { Id = Guid.NewGuid(), Code = code, Name = name, Unit = unit, MissionTypeId = missionTypeId,
            MaxQtyPerSlot = maxQtyPerSlot, Description = description, IsActive = true };
        db.Items.Add(item); AddAuditLog(userId, occurredAtUtc, "ITEM_CREATED", $"Item:{item.Id:D};Code:{code}");
        await db.SaveChangesAsync(cancellationToken);
        return new ItemCatalogRow(item.Id, item.Code, item.Name, item.Unit, item.MaxQtyPerSlot, item.IsActive, item.Description,
            item.MissionTypeId, missionTypeCode, missionTypeName);
    }

    public async Task<ItemCatalogRow> UpdateAsync(Guid itemId, string name, string unit, Guid missionTypeId,
        string missionTypeCode, string missionTypeName, int maxQtyPerSlot, string? description, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var item = await db.Items.Include(value => value.MissionType).SingleAsync(value => value.Id == itemId, cancellationToken);
        item.Name = name; item.Unit = unit; item.MissionTypeId = missionTypeId; item.MaxQtyPerSlot = maxQtyPerSlot; item.Description = description;
        AddAuditLog(userId, occurredAtUtc, "ITEM_UPDATED", $"Item:{item.Id:D}"); await db.SaveChangesAsync(cancellationToken);
        return new ItemCatalogRow(item.Id, item.Code, item.Name, item.Unit, item.MaxQtyPerSlot, item.IsActive, item.Description,
            item.MissionTypeId, missionTypeCode, missionTypeName);
    }

    public async Task<ItemCatalogRow> UpdateStatusAsync(Guid itemId, bool isActive, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var item = await db.Items.Include(value => value.MissionType).SingleAsync(value => value.Id == itemId, cancellationToken);
        item.IsActive = isActive; AddAuditLog(userId, occurredAtUtc, "ITEM_STATUS_UPDATED", $"Item:{item.Id:D};Active:{isActive}");
        await db.SaveChangesAsync(cancellationToken); return ToRow(item);
    }

    private void AddAuditLog(Guid userId, DateTime occurredAtUtc, string action, string targetObject) => db.ActivityLogs.Add(new ActivityLog
    { UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), Action = action, TargetObject = targetObject });

    private static ItemCatalogRow ToRow(Item item) => new(item.Id, item.Code, item.Name, item.Unit, item.MaxQtyPerSlot,
        item.IsActive, item.Description, item.MissionTypeId, item.MissionType.Code, item.MissionType.Name);
}
