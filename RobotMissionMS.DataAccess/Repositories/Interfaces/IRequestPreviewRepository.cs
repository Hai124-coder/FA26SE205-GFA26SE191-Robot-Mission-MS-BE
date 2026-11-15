namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

/// <summary>
/// Read-only data required to validate and split one manual request.
/// </summary>
public interface IRequestPreviewRepository
{
    Task<RequestPreviewData?> GetPreviewDataAsync(Guid userId, string departmentCode, string itemCode,
        CancellationToken cancellationToken = default);
}

public sealed record RequestPreviewData(
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
    bool MissionTypeIsActive,
    bool IsBatchMissionType,
    string? PickupLocationCode,
    string? PickupLocationName);
