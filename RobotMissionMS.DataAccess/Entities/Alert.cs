using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

public partial class Alert
{
    public Guid Id { get; set; }

    /// <summary>
    /// e.g. ROBOT_OFFLINE, NOT_RECEIVED, LOW_BATTERY, BLOCKED
    /// </summary>
    public string AlertType { get; set; } = null!;

    public Guid? ZoneId { get; set; }

    public Guid? RobotId { get; set; }

    public Guid? MissionId { get; set; }

    public Guid? TripId { get; set; }

    public string Message { get; set; } = null!;

    public Guid? AcknowledgedByUserId { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? AcknowledgedByUser { get; set; }

    public virtual Mission? Mission { get; set; }

    public virtual User? ResolvedByUser { get; set; }

    public virtual Robot? Robot { get; set; }

    public virtual Trip? Trip { get; set; }

    public virtual Zone? Zone { get; set; }
}
