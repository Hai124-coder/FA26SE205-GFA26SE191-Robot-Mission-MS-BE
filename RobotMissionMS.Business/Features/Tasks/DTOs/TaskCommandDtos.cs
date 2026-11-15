using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Tasks.DTOs;

public sealed class UpdateTaskRequest
{
    [Range(1, 600)] public int? Quantity { get; init; }
    [StringLength(10)] public string? Priority { get; init; }
    [StringLength(5)] public string? Deadline { get; init; }
    [StringLength(1000)] public string? Note { get; init; }
}

public sealed class CancelTaskRequest
{
    [Required, StringLength(1000, MinimumLength = 1)] public string? Reason { get; init; }
}

public sealed class TaskActionResponse
{
    public required Guid TaskId { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
