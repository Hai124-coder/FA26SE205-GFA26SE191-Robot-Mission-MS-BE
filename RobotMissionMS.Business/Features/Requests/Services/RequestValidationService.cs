using System.ComponentModel.DataAnnotations;
using System.Globalization;
using RobotMissionMS.Business.Features.Requests.DTOs;
using RobotMissionMS.DataAccess.Enums;
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
        if (!Enum.TryParse<MissionPriority>(request.Priority.Trim(), true, out var priority)
            || !Enum.IsDefined(priority))
            return RequestValidationResult.Failure(RequestPreviewError.InvalidPriority);
        if (!TryGetDeadlineUtc(request.Deadline, out var deadlineAt))
            return RequestValidationResult.Failure(RequestPreviewError.InvalidDeadline);
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
        return RequestValidationResult.Success(data, priority, deadlineAt, request.Note?.Trim());
    }

    private static bool TryGetDeadlineUtc(string? deadline, out DateTime? deadlineAt)
    {
        deadlineAt = null;
        if (string.IsNullOrWhiteSpace(deadline)) return true;
        if (!TimeOnly.TryParseExact(deadline, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false;
        var timeZone = GetVietnamTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var localDeadline = DateTime.SpecifyKind(localNow.Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
        deadlineAt = TimeZoneInfo.ConvertTimeToUtc(localDeadline, timeZone);
        return true;
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }
}

public sealed class RequestValidationResult
{
    private RequestValidationResult(RequestPreviewData? data, MissionPriority? priority, DateTime? deadlineAt, string? note, RequestPreviewError? error)
        => (Data, Priority, DeadlineAt, Note, Error) = (data, priority, deadlineAt, note, error);
    public RequestPreviewData? Data { get; }
    public MissionPriority? Priority { get; }
    public DateTime? DeadlineAt { get; }
    public string? Note { get; }
    public RequestPreviewError? Error { get; }
    public bool IsSuccess => Data is not null;
    public static RequestValidationResult Success(RequestPreviewData data, MissionPriority priority, DateTime? deadlineAt, string? note)
        => new(data, priority, deadlineAt, note, null);
    public static RequestValidationResult Failure(RequestPreviewError error) => new(null, null, null, null, error);
}
