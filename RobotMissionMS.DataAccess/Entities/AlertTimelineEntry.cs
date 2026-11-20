using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Immutable audit trail for the incident workflow of an alert.
/// </summary>
public class AlertTimelineEntry
{
    public Guid Id { get; set; }
    public Guid AlertId { get; set; }
    public IncidentTimelineEventType EventType { get; set; }
    public IncidentAction? Action { get; set; }
    public IncidentPhase? FromPhase { get; set; }
    public IncidentPhase? ToPhase { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? Note { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime OccurredAt { get; set; }

    public virtual Alert Alert { get; set; } = null!;
    public virtual User? ActorUser { get; set; }
}
