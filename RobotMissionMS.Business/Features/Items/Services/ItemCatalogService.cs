using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Items.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Items.Services;

public sealed class ItemCatalogService(IItemCatalogRepository repository, IMissionTypeRepository missionTypeRepository) : IItemCatalogService
{
    public async Task<ItemListResult> GetItemsAsync(ItemListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true)) return ItemListResult.Failure();
        var rows = await repository.GetItemsAsync(string.IsNullOrWhiteSpace(query.MissionType) ? null : query.MissionType.Trim(),
            query.Active, cancellationToken);
        return ItemListResult.Success(rows.Select(ToResponse).ToList());
    }

    public async Task<CreateItemResult> CreateAsync(Guid userId, CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || !IsValid(request)) return CreateItemResult.Failure(CreateItemError.InvalidRequest);
        var code = request.Code.Trim();
        if (await repository.FindByCodeAsync(code, cancellationToken) is not null) return CreateItemResult.Failure(CreateItemError.DuplicateCode);
        var missionType = await missionTypeRepository.FindByCodeAsync(request.MissionType.Trim(), cancellationToken);
        if (missionType is null) return CreateItemResult.Failure(CreateItemError.MissionTypeNotFound);
        var row = await repository.CreateAsync(code, request.Name.Trim(), request.Unit.Trim(), missionType.Id, missionType.Code,
            missionType.Name, request.MaxQtyPerSlot, request.Description?.Trim(), userId, DateTime.UtcNow, cancellationToken);
        return CreateItemResult.Success(ToResponse(row));
    }

    public async Task<UpdateItemResult> UpdateAsync(Guid userId, string code, UpdateItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code) || !IsValid(request)) return UpdateItemResult.Failure(UpdateItemError.InvalidRequest);
        var item = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (item is null) return UpdateItemResult.Failure(UpdateItemError.NotFound);
        var missionType = await missionTypeRepository.FindByCodeAsync(request.MissionType.Trim(), cancellationToken);
        if (missionType is null) return UpdateItemResult.Failure(UpdateItemError.MissionTypeNotFound);
        if (item.MissionTypeId != missionType.Id && await repository.HasMissionsAsync(item.Id, cancellationToken))
            return UpdateItemResult.Failure(UpdateItemError.MissionTypeChangeNotAllowed);
        var row = await repository.UpdateAsync(item.Id, request.Name.Trim(), request.Unit.Trim(), missionType.Id, missionType.Code,
            missionType.Name, request.MaxQtyPerSlot, request.Description?.Trim(), userId, DateTime.UtcNow, cancellationToken);
        return UpdateItemResult.Success(ToResponse(row));
    }

    public async Task<UpdateItemStatusResult> UpdateStatusAsync(Guid userId, string code, UpdateItemStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code) || request.Active is null)
            return UpdateItemStatusResult.Failure(UpdateItemStatusError.InvalidRequest);
        var item = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (item is null) return UpdateItemStatusResult.Failure(UpdateItemStatusError.NotFound);
        return UpdateItemStatusResult.Success(ToResponse(await repository.UpdateStatusAsync(item.Id, request.Active.Value, userId,
            DateTime.UtcNow, cancellationToken)));
    }

    private static bool IsValid(object request) => Validator.TryValidateObject(request, new ValidationContext(request), null, true);
    private static ItemResponse ToResponse(ItemCatalogRow row) => new()
    {
        Id = row.Id, Code = row.Code, Name = row.Name, MissionType = row.MissionTypeCode, MissionTypeName = row.MissionTypeName,
        Unit = row.Unit, MaxQtyPerSlot = row.MaxQtyPerSlot, Description = row.Description, IsActive = row.IsActive
    };
}
