using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Requests.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Requests.Services;

public sealed class RequestValidationService(IRequestPreviewRepository repository)
{
    public async Task<RequestValidationResult> ValidateAsync(Guid operatorUserId, RequestPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (operatorUserId == Guid.Empty || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)
            || string.IsNullOrWhiteSpace(request.Department) || string.IsNullOrWhiteSpace(request.ItemCode))
            return RequestValidationResult.Failure(RequestPreviewError.InvalidRequest);
        var data = await repository.GetPreviewDataAsync(operatorUserId, request.Department.Trim(), request.ItemCode.Trim(), cancellationToken);
        if (data?.OperatorFloorCode is null) return RequestValidationResult.Failure(RequestPreviewError.OperatorFloorNotConfigured);
        if (data.OperatorFloorIsActive != true) return RequestValidationResult.Failure(RequestPreviewError.OperatorFloorInactive);
        if (data.Department is null) return RequestValidationResult.Failure(RequestPreviewError.DepartmentNotFound);
        if (!data.Department.IsActive) return RequestValidationResult.Failure(RequestPreviewError.DepartmentInactive);
        if (!string.Equals(data.Department.FloorCode, data.OperatorFloorCode, StringComparison.Ordinal)) return RequestValidationResult.Failure(RequestPreviewError.FloorForbidden);
        if (data.Item is null) return RequestValidationResult.Failure(RequestPreviewError.ItemNotFound);
        if (!data.Item.IsActive) return RequestValidationResult.Failure(RequestPreviewError.ItemInactive);
        if (!data.Item.MissionTypeIsActive) return RequestValidationResult.Failure(RequestPreviewError.MissionTypeInactive);
        if (!data.Item.IsBatchMissionType) return RequestValidationResult.Failure(RequestPreviewError.MissionTypeNotAvailableForManualRequest);
        if (data.Item.PickupLocationId is null || data.Item.PickupLocationCode is null || data.Item.PickupLocationName is null)
            return RequestValidationResult.Failure(RequestPreviewError.PickupLocationNotConfigured);
        if (data.Item.KgPerUnit is null || data.Item.KgPerUnit <= 0 || data.Item.MaxQtyPerSlot <= 0)
            return RequestValidationResult.Failure(RequestPreviewError.ItemWeightNotConfigured);
        return RequestValidationResult.Success(data);
    }
}

public sealed class RequestValidationResult
{
    private RequestValidationResult(RequestPreviewData? data, RequestPreviewError? error) => (Data, Error) = (data, error);
    public RequestPreviewData? Data { get; }
    public RequestPreviewError? Error { get; }
    public bool IsSuccess => Data is not null;
    public static RequestValidationResult Success(RequestPreviewData data) => new(data, null);
    public static RequestValidationResult Failure(RequestPreviewError error) => new(null, error);
}
