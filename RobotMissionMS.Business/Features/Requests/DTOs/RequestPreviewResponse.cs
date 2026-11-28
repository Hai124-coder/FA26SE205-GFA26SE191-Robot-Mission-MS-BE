namespace RobotMissionMS.Business.Features.Requests.DTOs;

public sealed class RequestPreviewResponse
{
    public required string Floor { get; init; }
    public required RequestPreviewLocationResponse Department { get; init; }
    public required RequestPreviewItemResponse Item { get; init; }
    public required RequestPreviewLocationResponse PickupLocation { get; init; }
    public required int Quantity { get; init; }
    public required string Priority { get; init; }
    public DateTimeOffset? DeadlineAt { get; init; }
    /// <summary>Echoed back from the request so the caller can confirm what will be stored.</summary>
    public string? Note { get; init; }
    public required int TaskCount { get; init; }
    public required decimal TotalWeightKg { get; init; }
    public required IReadOnlyList<RequestPreviewSplitResponse> Splits { get; init; }
}

public sealed class RequestPreviewLocationResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
}

public sealed class RequestPreviewItemResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string MissionType { get; init; }
    public required string MissionTypeName { get; init; }
    public required int MaxQtyPerSlot { get; init; }
    public required decimal KgPerUnit { get; init; }
}

public sealed class RequestPreviewSplitResponse
{
    public required int SlotNo { get; init; }
    public required int Quantity { get; init; }
    public required decimal WeightKg { get; init; }
}
