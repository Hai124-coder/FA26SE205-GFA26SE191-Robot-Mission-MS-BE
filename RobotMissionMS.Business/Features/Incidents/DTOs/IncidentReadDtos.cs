using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Incidents.DTOs;

public sealed class IncidentListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(100)] public string? Status { get; init; }
    [StringLength(100)] public string? Category { get; init; }
    [StringLength(50)] public string? Type { get; init; }
    public Guid? RobotId { get; init; }
    [StringLength(50)] public string? Floor { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public sealed class IncidentStatsQuery
{
    [StringLength(50)] public string? Floor { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public class IncidentListItemResponse
{
    public required Guid Id { get; init; }
    public string? ScenarioCode { get; init; }
    public required string Type { get; init; }
    public required string Category { get; init; }
    public required string Severity { get; init; }
    public required string Status { get; init; }
    public string? Phase { get; init; }
    public string? CargoState { get; init; }
    public string? Floor { get; init; }
    public Guid? RobotId { get; init; }
    public string? RobotCode { get; init; }
    public Guid? TripId { get; init; }
    public string? TripCode { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}

public sealed class IncidentDetailResponse : IncidentListItemResponse
{
    public string? SuggestedAction { get; init; }
    public string? ResolutionNote { get; init; }
    public string? ResolvedBy { get; init; }
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public IReadOnlyList<IncidentMissionResponse> Missions { get; init; } = [];
    public IReadOnlyList<IncidentTimelineItemResponse> Timeline { get; init; } = [];
}

public sealed class IncidentMissionResponse
{
    public required Guid Id { get; init; }
    public required string MissionCode { get; init; }
    public required string Status { get; init; }
}

public sealed class IncidentTimelineItemResponse
{
    public required Guid Id { get; init; }
    public required string EventType { get; init; }
    public string? Action { get; init; }
    public string? FromPhase { get; init; }
    public string? ToPhase { get; init; }
    public string? Actor { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class IncidentStatsResponse
{
    public int OpenCount { get; init; }
    public int ResolvedCount { get; init; }
    public int ResolvedByOperatorCount { get; init; }
    public int AutoResolvedCount { get; init; }
    public double? AverageResolutionMinutes { get; init; }
    public int HumanCompletedMissionCount { get; init; }
}
