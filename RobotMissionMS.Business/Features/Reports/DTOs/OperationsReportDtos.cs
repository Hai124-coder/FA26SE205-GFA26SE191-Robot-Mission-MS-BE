using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Reports.DTOs;

public sealed class OperationsReportQuery
{
    [Required] public DateTime? From { get; init; }
    [Required] public DateTime? To { get; init; }
    [StringLength(50)] public string? Floor { get; init; }
    [RegularExpression("(?i)^(hour|day)$")] public string GroupBy { get; init; } = "day";
}

public sealed class OperationsReportResponse
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public string? Floor { get; init; }
    public required string GroupBy { get; init; }
    public required IReadOnlyList<OperationsReportBucket> Buckets { get; init; }
}

public sealed class OperationsReportBucket
{
    public required DateTime StartAt { get; init; }
    public required IReadOnlyList<OperationsMissionTypeCount> MissionTypes { get; init; }
    public required OperationsOutcomeCounts Outcomes { get; init; }
    public required OperationsOnTimeMetrics OnTime { get; init; }
    public double? AverageDeliverySeconds { get; init; }
    public required IReadOnlyList<OperationsFailureReasonCount> FailureReasons { get; init; }
    public required int CompletedByHumanCount { get; init; }
}

public sealed record OperationsMissionTypeCount(string Code, int Total);
public sealed record OperationsFailureReasonCount(string Reason, int Count);
public sealed class OperationsOutcomeCounts
{
    public required int Completed { get; init; }
    public required int CompletedByHuman { get; init; }
    public required int DeliveryFailed { get; init; }
    public required int Cancelled { get; init; }
    public required int Pending { get; init; }
    public required int Assigned { get; init; }
    public required int InProgress { get; init; }
    public required int Rescheduled { get; init; }
}
public sealed class OperationsOnTimeMetrics { public required int Eligible { get; init; } public required int OnTime { get; init; } public decimal? Rate { get; init; } }
