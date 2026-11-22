namespace RobotMissionMS.Business.Features.Dashboard.DTOs;

public sealed class DashboardOverviewResponse
{
    public required DateTime GeneratedAt { get; init; }
    public required DateOnly BusinessDate { get; init; }
    public required DashboardRobotCounts RobotCounts { get; init; }
    public required DashboardMissionCounts MissionCounts { get; init; }
    public required int QueueCount { get; init; }
    public required int OpenIncidentCount { get; init; }
    public required DashboardOnTimeMetrics OnTime { get; init; }
}

public sealed class DashboardRobotCounts
{
    public required int OnTrip { get; init; }
    public required int Charging { get; init; }
    public required int Available { get; init; }
    public required int Maintenance { get; init; }
    public required int Error { get; init; }
    public required int Inactive { get; init; }
}

public sealed class DashboardMissionCounts
{
    public required int Pending { get; init; }
    public required int Assigned { get; init; }
    public required int InProgress { get; init; }
    public required int DeliveryFailed { get; init; }
    public required int Completed { get; init; }
    public required int CompletedByHuman { get; init; }
    public required int Rescheduled { get; init; }
    public required int Cancelled { get; init; }
}

public sealed class DashboardOnTimeMetrics
{
    public required int Eligible { get; init; }
    public required int OnTime { get; init; }
    public decimal? Rate { get; init; }
}
