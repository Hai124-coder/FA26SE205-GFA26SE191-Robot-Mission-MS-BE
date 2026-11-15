using RobotMissionMS.Business.Features.Requests.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Requests.Services;

public sealed class RequestService(IRequestPreviewRepository repository, RequestValidationService validator) : IRequestService
{
    public async Task<RequestCreateResult> CreateAsync(Guid operatorUserId, RequestPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await validator.ValidateAsync(operatorUserId, request, cancellationToken);
        if (!validation.IsSuccess) return RequestCreateResult.Failure(validation.Error!.Value);
        var data = validation.Data!;
        var item = data.Item!;
        var department = data.Department!;
        var requestCode = $"YC-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
        var createdAt = TruncateToMicroseconds(DateTime.UtcNow);
        var remaining = request.Quantity;
        var missions = new List<ManualMissionCreateData>();
        while (remaining > 0)
        {
            var quantity = Math.Min(remaining, item.MaxQtyPerSlot);
            var slotNo = missions.Count + 1;
            missions.Add(new ManualMissionCreateData($"NV-{requestCode[3..]}-{slotNo:D2}", quantity,
                quantity * item.KgPerUnit!.Value, slotNo));
            remaining -= quantity;
        }
        var zoneId = data.OperatorZoneId ?? throw new InvalidOperationException("Operator zone ID is missing after validation.");
        var created = await repository.CreateManualRequestAsync(new ManualRequestCreateData(operatorUserId, zoneId, requestCode,
            createdAt, department, item, missions), cancellationToken);
        return RequestCreateResult.Success(ToResponse(created!));
    }

    public async Task<RequestReadResult> GetByCodeAsync(string requestCode, bool operatorOnly, string? operatorFloor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestCode) || requestCode.Length > 50) return RequestReadResult.Failure(RequestReadError.InvalidRequestCode);
        var found = await repository.FindRequestByCodeAsync(requestCode.Trim(), cancellationToken);
        if (found is null) return RequestReadResult.Failure(RequestReadError.NotFound);
        if (operatorOnly && !string.Equals(found.FloorCode, operatorFloor, StringComparison.Ordinal))
            return RequestReadResult.Failure(RequestReadError.FloorForbidden);
        return RequestReadResult.Success(ToResponse(found));
    }

    private static DateTime TruncateToMicroseconds(DateTime timestamp) => new(timestamp.Ticks - timestamp.Ticks % 10, DateTimeKind.Utc);
    private static RequestDetailResponse ToResponse(RequestDetailData value) => new()
    {
        RequestCode = value.RequestCode, Floor = value.FloorCode, FloorName = value.FloorName, Source = value.Source switch
        {
            BatchSource.Manual => "MANUAL", BatchSource.Import => "IMPORT", _ => throw new InvalidOperationException("Unknown request source.")
        },
        CreatedAt = new DateTimeOffset(value.CreatedAt), CreatedBy = value.CreatedByUsername, Note = value.Note,
        Missions = value.Missions.Select(mission => new RequestMissionResponse
        {
            MissionCode = mission.MissionCode, Status = ToStatusCode(mission.Status), MissionType = mission.MissionType,
            MissionTypeName = mission.MissionTypeName, PickupLocation = new RequestPreviewLocationResponse { Code = mission.FromLocationCode, Name = mission.FromLocationName },
            Department = new RequestPreviewLocationResponse { Code = mission.ToLocationCode, Name = mission.ToLocationName },
            ItemCode = mission.ItemCode, ItemName = mission.ItemName, Unit = mission.Unit, Packaging = mission.Packaging,
            KgPerUnit = mission.KgPerUnit, WeightKg = mission.WeightKg, Quantity = mission.Quantity, SlotNo = mission.SlotNo,
            CreatedAt = new DateTimeOffset(mission.CreatedAt)
        }).ToList()
    };

    private static string ToStatusCode(MissionStatus status) => status switch
    {
        MissionStatus.Pending => "PENDING", MissionStatus.Assigned => "ASSIGNED", MissionStatus.InProgress => "IN_PROGRESS",
        MissionStatus.DeliveryFailed => "DELIVERY_FAILED", MissionStatus.Completed => "COMPLETED",
        MissionStatus.CompletedByHuman => "COMPLETED_BY_HUMAN", MissionStatus.Rescheduled => "RESCHEDULED",
        MissionStatus.Cancelled => "CANCELLED", _ => throw new InvalidOperationException("Unknown mission status.")
    };
}
