namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IItemCatalogRepository
{
    Task<IReadOnlyList<ItemCatalogRow>> GetItemsAsync(string? missionTypeCode, bool? isActive,
        CancellationToken cancellationToken = default);
}

public sealed record ItemCatalogRow(Guid Id, string Code, string Name, string Unit, int MaxQtyPerSlot, bool IsActive,
    string? Description, string MissionTypeCode, string MissionTypeName);
