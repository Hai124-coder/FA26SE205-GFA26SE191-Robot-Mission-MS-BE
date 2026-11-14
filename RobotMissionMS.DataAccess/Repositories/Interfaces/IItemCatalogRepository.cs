namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IItemCatalogRepository
{
    Task<IReadOnlyList<ItemCatalogRow>> GetItemsAsync(string? missionTypeCode, bool? isActive,
        CancellationToken cancellationToken = default);
    Task<ItemCatalogRow?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> HasMissionsAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<ItemCatalogRow> CreateAsync(string code, string name, string unit, Guid missionTypeId, string missionTypeCode,
        string missionTypeName, int maxQtyPerSlot, string? description, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
    Task<ItemCatalogRow> UpdateAsync(Guid itemId, string name, string unit, Guid missionTypeId, string missionTypeCode,
        string missionTypeName, int maxQtyPerSlot, string? description, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
    Task<ItemCatalogRow> UpdateStatusAsync(Guid itemId, bool isActive, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record ItemCatalogRow(Guid Id, string Code, string Name, string Unit, int MaxQtyPerSlot, bool IsActive,
    string? Description, Guid MissionTypeId, string MissionTypeCode, string MissionTypeName);
