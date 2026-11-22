using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Common.DTOs;

namespace RobotMissionMS.Business.Features.ActivityLogs.DTOs;

public sealed class ActivityLogQuery
{
    public Guid? Actor { get; init; }

    [StringLength(100)]
    public string? Action { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    [StringLength(255)]
    public string? Q { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class ActivityLogEntryResponse
{
    public required long Id { get; init; }
    public required DateTime Timestamp { get; init; }
    public ActivityLogActorResponse? Actor { get; init; }
    public required string Action { get; init; }
    public required string TargetObject { get; init; }
    public required string Message { get; init; }
}

public sealed class ActivityLogActorResponse
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string FullName { get; init; }
}

public sealed record ActivityLogPage(PagedResponse<ActivityLogEntryResponse>? Response, ActivityLogQueryError? Error)
{
    public bool IsSuccess => Error is null;
    public static ActivityLogPage Success(PagedResponse<ActivityLogEntryResponse> response) => new(response, null);
    public static ActivityLogPage Failure(ActivityLogQueryError error) => new(null, error);
}

public sealed record ActivityLogExport(IReadOnlyList<ActivityLogEntryResponse>? Items, ActivityLogQueryError? Error)
{
    public bool IsSuccess => Error is null;
    public static ActivityLogExport Success(IReadOnlyList<ActivityLogEntryResponse> items) => new(items, null);
    public static ActivityLogExport Failure(ActivityLogQueryError error) => new(null, error);
}

public enum ActivityLogQueryError { InvalidQuery, RangeTooLarge, ExportLimitExceeded }
