using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

/// <summary>
/// Read-only data required to validate and split one manual request.
/// </summary>
public interface IRequestPreviewRepository
{
    Task<RequestPreviewData?> GetPreviewDataAsync(Guid userId, string departmentCode, string itemCode,
        CancellationToken cancellationToken = default);
    Task<RequestDetailData?> CreateManualRequestAsync(ManualRequestCreateData request,
        CancellationToken cancellationToken = default);
    Task<RequestDetailData?> FindRequestByCodeAsync(string requestCode, CancellationToken cancellationToken = default);
}

public sealed record RequestPreviewData(
    Guid? OperatorZoneId,
    string? OperatorFloorCode,
    bool? OperatorFloorIsActive,
    RequestPreviewDepartment? Department,
    RequestPreviewItem? Item);

public sealed record RequestPreviewDepartment(Guid Id, string Code, string Name, string FloorCode, bool IsActive);

public sealed record RequestPreviewItem(
    Guid Id,
    string Code,
    string Name,
    string Unit,
    bool IsActive,
    int MaxQtyPerSlot,
    decimal? KgPerUnit,
    string MissionTypeCode,
    string MissionTypeName,
    Guid MissionTypeId,
    bool MissionTypeIsActive,
    bool IsBatchMissionType,
    Guid? PickupLocationId,
    string? PickupLocationCode,
    string? PickupLocationName,
    string? Packaging);

public sealed record ManualRequestCreateData(Guid CreatedByUserId, Guid ZoneId, string RequestCode, DateTime CreatedAt,
    RequestPreviewDepartment Department, RequestPreviewItem Item, IReadOnlyList<ManualMissionCreateData> Missions);
public sealed record ManualMissionCreateData(string MissionCode, int Quantity, decimal WeightKg, int SlotNo);

public sealed record RequestDetailData(string RequestCode, string FloorCode, string FloorName, BatchSource Source, DateTime CreatedAt,
    string CreatedByUsername, string? Note, IReadOnlyList<RequestMissionDetailData> Missions);
public sealed record RequestMissionDetailData(string MissionCode, MissionStatus Status, string MissionType, string MissionTypeName,
    string FromLocationCode, string FromLocationName, string ToLocationCode, string ToLocationName, string ItemCode,
    string ItemName, string Unit, string? Packaging, decimal KgPerUnit, decimal WeightKg, int Quantity, int SlotNo, DateTime CreatedAt);
