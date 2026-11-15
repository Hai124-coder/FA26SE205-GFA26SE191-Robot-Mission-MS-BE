using RobotMissionMS.Business.Features.Requests.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Requests.Services;

public sealed class RequestPreviewService(RequestValidationService validator) : IRequestPreviewService
{
    public async Task<RequestPreviewResult> PreviewAsync(Guid operatorUserId, RequestPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await validator.ValidateAsync(operatorUserId, request, cancellationToken);
        if (!validation.IsSuccess) return RequestPreviewResult.Failure(validation.Error!.Value);
        var data = validation.Data ?? throw new InvalidOperationException("Successful validation did not include request data.");
        var item = data.Item ?? throw new InvalidOperationException("Successful validation did not include item data.");
        var department = data.Department ?? throw new InvalidOperationException("Successful validation did not include department data.");
        var floor = data.OperatorFloorCode ?? throw new InvalidOperationException("Successful validation did not include floor data.");
        var pickupCode = data.Item.PickupLocationCode ?? throw new InvalidOperationException("Successful validation did not include pickup data.");
        var pickupName = data.Item.PickupLocationName ?? throw new InvalidOperationException("Successful validation did not include pickup data.");
        var kgPerUnit = item.KgPerUnit ?? throw new InvalidOperationException("Successful validation did not include item weight.");
        var priority = validation.Priority ?? throw new InvalidOperationException("Successful validation did not include priority.");

        var remaining = request.Quantity;
        var splits = new List<RequestPreviewSplitResponse>();
        while (remaining > 0)
        {
            var quantity = Math.Min(remaining, item.MaxQtyPerSlot);
            splits.Add(new RequestPreviewSplitResponse { SlotNo = splits.Count + 1, Quantity = quantity,
                WeightKg = quantity * kgPerUnit });
            remaining -= quantity;
        }

        return RequestPreviewResult.Success(new RequestPreviewResponse
        {
            Floor = floor,
            Department = new RequestPreviewLocationResponse { Code = department.Code, Name = department.Name },
            Item = new RequestPreviewItemResponse { Code = item.Code, Name = item.Name, Unit = item.Unit,
                MissionType = item.MissionTypeCode, MissionTypeName = item.MissionTypeName,
                MaxQtyPerSlot = item.MaxQtyPerSlot, KgPerUnit = kgPerUnit },
            PickupLocation = new RequestPreviewLocationResponse { Code = pickupCode, Name = pickupName },
            Quantity = request.Quantity, Priority = priority.ToString().ToUpperInvariant(),
            DeadlineAt = validation.DeadlineAt is { } deadlineAt ? new DateTimeOffset(deadlineAt) : null,
            TaskCount = splits.Count, TotalWeightKg = request.Quantity * kgPerUnit,
            Splits = splits
        });
    }
}
