using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Requests.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Requests.Services;

public sealed class RequestPreviewService(IRequestPreviewRepository repository) : IRequestPreviewService
{
    public async Task<RequestPreviewResult> PreviewAsync(Guid operatorUserId, RequestPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (operatorUserId == Guid.Empty || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)
            || string.IsNullOrWhiteSpace(request.Department) || string.IsNullOrWhiteSpace(request.ItemCode))
            return RequestPreviewResult.Failure(RequestPreviewError.InvalidRequest);

        var data = await repository.GetPreviewDataAsync(operatorUserId, request.Department.Trim(), request.ItemCode.Trim(), cancellationToken);
        if (data?.OperatorFloorCode is null) return RequestPreviewResult.Failure(RequestPreviewError.OperatorFloorNotConfigured);
        if (data.OperatorFloorIsActive != true) return RequestPreviewResult.Failure(RequestPreviewError.OperatorFloorInactive);
        if (data.Department is null) return RequestPreviewResult.Failure(RequestPreviewError.DepartmentNotFound);
        if (!data.Department.IsActive) return RequestPreviewResult.Failure(RequestPreviewError.DepartmentInactive);
        if (!string.Equals(data.Department.FloorCode, data.OperatorFloorCode, StringComparison.Ordinal))
            return RequestPreviewResult.Failure(RequestPreviewError.FloorForbidden);
        if (data.Item is null) return RequestPreviewResult.Failure(RequestPreviewError.ItemNotFound);
        if (!data.Item.IsActive) return RequestPreviewResult.Failure(RequestPreviewError.ItemInactive);
        if (!data.Item.MissionTypeIsActive) return RequestPreviewResult.Failure(RequestPreviewError.MissionTypeInactive);
        if (!data.Item.IsBatchMissionType) return RequestPreviewResult.Failure(RequestPreviewError.MissionTypeNotAvailableForManualRequest);
        if (data.Item.PickupLocationCode is null || data.Item.PickupLocationName is null)
            return RequestPreviewResult.Failure(RequestPreviewError.PickupLocationNotConfigured);
        if (data.Item.KgPerUnit is null || data.Item.KgPerUnit <= 0 || data.Item.MaxQtyPerSlot <= 0)
            return RequestPreviewResult.Failure(RequestPreviewError.ItemWeightNotConfigured);

        var remaining = request.Quantity;
        var splits = new List<RequestPreviewSplitResponse>();
        while (remaining > 0)
        {
            var quantity = Math.Min(remaining, data.Item.MaxQtyPerSlot);
            splits.Add(new RequestPreviewSplitResponse { SlotNo = splits.Count + 1, Quantity = quantity,
                WeightKg = quantity * data.Item.KgPerUnit.Value });
            remaining -= quantity;
        }

        return RequestPreviewResult.Success(new RequestPreviewResponse
        {
            Floor = data.OperatorFloorCode,
            Department = new RequestPreviewLocationResponse { Code = data.Department.Code, Name = data.Department.Name },
            Item = new RequestPreviewItemResponse { Code = data.Item.Code, Name = data.Item.Name, Unit = data.Item.Unit,
                MissionType = data.Item.MissionTypeCode, MissionTypeName = data.Item.MissionTypeName,
                MaxQtyPerSlot = data.Item.MaxQtyPerSlot, KgPerUnit = data.Item.KgPerUnit.Value },
            PickupLocation = new RequestPreviewLocationResponse { Code = data.Item.PickupLocationCode, Name = data.Item.PickupLocationName },
            Quantity = request.Quantity, TaskCount = splits.Count, TotalWeightKg = request.Quantity * data.Item.KgPerUnit.Value,
            Splits = splits
        });
    }
}
