using RobotMissionMS.Business.Features.Items.DTOs;

namespace RobotMissionMS.Business.Features.Items.Services;

public interface IItemCatalogService
{
    Task<ItemListResult> GetItemsAsync(ItemListQuery query, CancellationToken cancellationToken = default);
    Task<CreateItemResult> CreateAsync(Guid userId, CreateItemRequest request, CancellationToken cancellationToken = default);
    Task<UpdateItemResult> UpdateAsync(Guid userId, string code, UpdateItemRequest request, CancellationToken cancellationToken = default);
    Task<UpdateItemStatusResult> UpdateStatusAsync(Guid userId, string code, UpdateItemStatusRequest request,
        CancellationToken cancellationToken = default);
}
