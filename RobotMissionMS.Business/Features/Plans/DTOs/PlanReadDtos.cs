using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Plans.DTOs;

public sealed class PlanCurrentQuery { [StringLength(50)] public string? Floor { get; init; } }
public sealed class PlanListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(50)] public string? Floor { get; init; }
    [StringLength(100)] public string? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public sealed class TripListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(100)] public string? Status { get; init; }
    public Guid? RobotId { get; init; }
    [StringLength(50)] public string? Floor { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public class PlanSummaryResponse
{
    public required Guid Id { get; init; }
    public required string Floor { get; init; }
    public required string Status { get; init; }
    public required string Source { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
    public int ManualEditCount { get; init; }
    public long Version { get; init; }
    public int TripCount { get; init; }
    public int AssignedTaskCount { get; init; }
    public int WaitingTaskCount { get; init; }
    public int EstimatedLateTaskCount { get; init; }
    public decimal? EstimatedDistanceM { get; init; }
    public decimal? BaselineDistanceM { get; init; }
    public int? EstimatedDurationSec { get; init; }
    public int? BaselineDurationSec { get; init; }
}

public sealed class PlanDetailResponse : PlanSummaryResponse
{
    public IReadOnlyList<PlanTripResponse> Trips { get; init; } = [];
    public IReadOnlyList<PlanTaskResponse> Tasks { get; init; } = [];
}

public sealed class PlanTripResponse
{
    public required Guid Id { get; init; }
    public required int Sequence { get; init; }
    public required Guid RobotId { get; init; }
    public required string RobotCode { get; init; }
    public required string RobotType { get; init; }
    public string? PickupLocation { get; init; }
    public DateTimeOffset? EstimatedStartAt { get; init; }
    public DateTimeOffset? EstimatedEndAt { get; init; }
    public decimal? EstimatedDistanceM { get; init; }
    public IReadOnlyList<PlanStopResponse> Stops { get; init; } = [];
}

public sealed class PlanStopResponse
{
    public required Guid Id { get; init; }
    public required int Sequence { get; init; }
    public required string Location { get; init; }
    public required string Action { get; init; }
    public DateTimeOffset? EstimatedArrivedAt { get; init; }
    public DateTimeOffset? EstimatedDepartedAt { get; init; }
    public decimal? CumulativeDistanceM { get; init; }
}

public sealed class PlanTaskResponse
{
    public required Guid Id { get; init; }
    public required Guid MissionId { get; init; }
    public required string MissionCode { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public string? RobotCode { get; init; }
    public int? SlotNo { get; init; }
    public int? DeliveryStopSequence { get; init; }
    public DateTimeOffset? EstimatedDeliveryAt { get; init; }
    public string? WaitReason { get; init; }
    public string? LateReason { get; init; }
}

public class TripListItemResponse
{
    public required Guid Id { get; init; }
    public required string TripCode { get; init; }
    public required string Status { get; init; }
    public required string Floor { get; init; }
    public required Guid RobotId { get; init; }
    public required string RobotCode { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DispatchedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int TaskCount { get; init; }
}

public sealed class TripDetailResponse : TripListItemResponse
{
    public IReadOnlyList<TripStopResponse> Stops { get; init; } = [];
    public IReadOnlyList<TripTaskResponse> Tasks { get; init; } = [];
}

public sealed class TripStopResponse
{
    public required Guid Id { get; init; }
    public required int Sequence { get; init; }
    public required string Location { get; init; }
    public required string Action { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? ArrivedAt { get; init; }
    public DateTimeOffset? DepartedAt { get; init; }
}

public sealed class TripTaskResponse
{
    public required Guid MissionId { get; init; }
    public required string MissionCode { get; init; }
    public required string Status { get; init; }
    public required string MissionType { get; init; }
    public int? SlotNo { get; init; }
    public string? Department { get; init; }
}
