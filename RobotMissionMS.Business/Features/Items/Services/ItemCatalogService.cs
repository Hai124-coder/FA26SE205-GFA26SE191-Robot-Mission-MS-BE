using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Items.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Items.Services;

public sealed class ItemCatalogService(IItemCatalogRepository repository) : IItemCatalogService
{
    public async Task<ItemListResult> GetItemsAsync(ItemListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true)) return ItemListResult.Failure();
        var rows = await repository.GetItemsAsync(string.IsNullOrWhiteSpace(query.MissionType) ? null : query.MissionType.Trim(),
            query.Active, cancellationToken);
        return ItemListResult.Success(rows.Select(row => new ItemResponse
        {
            Id = row.Id, Code = row.Code, Name = row.Name, MissionType = row.MissionTypeCode, MissionTypeName = row.MissionTypeName,
            Unit = row.Unit, MaxQtyPerSlot = row.MaxQtyPerSlot, Description = row.Description, IsActive = row.IsActive
        }).ToList());
    }
}
