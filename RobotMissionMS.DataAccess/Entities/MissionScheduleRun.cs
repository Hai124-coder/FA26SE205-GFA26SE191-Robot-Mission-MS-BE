using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Entities;

public sealed partial class MissionScheduleRun
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public DateTime ScheduledFor { get; set; }
    public bool IsManual { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public ScheduleRunStatus Status { get; set; }
    public string RouteSnapshot { get; set; } = "{}";
    public Guid? MissionId { get; set; }
    public Guid? LateAlertId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastEvaluatedAt { get; set; }
    public DateTime? LateAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MissionSchedule Schedule { get; set; } = null!;
    public User? RequestedByUser { get; set; }
    public Mission? Mission { get; set; }
    public Alert? LateAlert { get; set; }
}
