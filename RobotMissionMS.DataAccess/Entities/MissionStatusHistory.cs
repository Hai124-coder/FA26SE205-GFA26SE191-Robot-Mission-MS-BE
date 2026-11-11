using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Mission (not robot) state transitions for tracking and audit.
/// </summary>
public partial class MissionStatusHistory
{
    public Guid Id { get; set; }

    public Guid MissionId { get; set; }

    public DateTime ChangedAt { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual Mission Mission { get; set; } = null!;
}
