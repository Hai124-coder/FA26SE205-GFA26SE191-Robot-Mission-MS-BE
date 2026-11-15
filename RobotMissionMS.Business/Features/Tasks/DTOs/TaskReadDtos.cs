using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Tasks.DTOs;

public sealed class TaskListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(200)] public string? Status { get; init; }
    [StringLength(50)] public string? Floor { get; init; }
    [StringLength(50)] public string? Type { get; init; }
    [StringLength(50)] public string? Department { get; init; }
    public Guid? RobotId { get; init; }
    [StringLength(50)] public string? RequestCode { get; init; }
    public Guid? ImportId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    [StringLength(100)] public string? Q { get; init; }
}

public class TaskListItemResponse
{
    public required Guid Id { get; init; }
    public required string MissionCode { get; init; }
    public string? RequestCode { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public required string MissionType { get; init; }
    public required string Floor { get; init; }
    public string? Department { get; init; }
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public int? Quantity { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeadlineAt { get; init; }
    public string? RobotCode { get; init; }
    public string? TripCode { get; init; }
}

public sealed class TaskDetailResponse : TaskListItemResponse
{
    public string? FromLocation { get; init; }
    public string? ToLocation { get; init; }
    public decimal? KgPerUnit { get; init; }
    public decimal? WeightKg { get; init; }
    public int? SlotNo { get; init; }
    public string? Note { get; init; }
    public string? FailReason { get; init; }
    public string? FailNote { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<TaskAlertResponse> Alerts { get; init; } = [];
    public IReadOnlyList<string> Actions { get; init; } = [];
}

public sealed class TaskAlertResponse
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Status { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class TaskTimelineResponse
{
    public required Guid TaskId { get; init; }
    public required IReadOnlyList<TaskTimelineItemResponse> Items { get; init; }
}

public sealed class TaskTimelineItemResponse
{
    public required string Event { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? FromStatus { get; init; }
    public string? ToStatus { get; init; }
    public string? Actor { get; init; }
    public string? Reason { get; init; }
}
