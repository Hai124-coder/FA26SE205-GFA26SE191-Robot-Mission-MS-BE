using RobotMissionMS.Business.Features.Items.DTOs;

namespace RobotMissionMS.Business.Features.Items.Services;

public interface IItemCatalogService
{
    Task<ItemListResult> GetItemsAsync(ItemListQuery query, CancellationToken cancellationToken = default);
}
