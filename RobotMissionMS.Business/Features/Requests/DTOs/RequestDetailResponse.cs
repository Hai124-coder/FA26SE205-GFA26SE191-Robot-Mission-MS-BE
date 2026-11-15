namespace RobotMissionMS.Business.Features.Requests.DTOs;

public sealed class RequestDetailResponse
{
    public required string RequestCode { get; init; }
    public required string Floor { get; init; }
    public required string FloorName { get; init; }
    public required string Source { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public string? Note { get; init; }
    public required IReadOnlyList<RequestMissionResponse> Missions { get; init; }
}

public sealed class RequestMissionResponse
{
    public required string MissionCode { get; init; }
    public required string Status { get; init; }
    public required string MissionType { get; init; }
    public required string MissionTypeName { get; init; }
    public required RequestPreviewLocationResponse PickupLocation { get; init; }
    public required RequestPreviewLocationResponse Department { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemName { get; init; }
    public required string Unit { get; init; }
    public string? Packaging { get; init; }
    public required decimal KgPerUnit { get; init; }
    public required decimal WeightKg { get; init; }
    public required int Quantity { get; init; }
    public required string Priority { get; init; }
    public DateTimeOffset? DeadlineAt { get; init; }
    public required int SlotNo { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
