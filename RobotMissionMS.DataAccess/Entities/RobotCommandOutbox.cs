using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Entities;

public sealed class RobotCommandOutbox
{
    public Guid Id { get; set; }
    public string CommandType { get; set; } = null!;
    public Guid RobotId { get; set; }
    public Guid? TripId { get; set; }
    public Guid? PlanId { get; set; }
    public string Topic { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public RobotCommandOutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Robot Robot { get; set; } = null!;
    public Trip? Trip { get; set; }
    public DispatchPlan? Plan { get; set; }
}
