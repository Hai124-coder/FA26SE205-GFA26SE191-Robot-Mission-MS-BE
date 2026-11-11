using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Stores user activity history, including map imports and order batching (WF-01, WF-02).
/// </summary>
public partial class ActivityLog
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Action { get; set; } = null!;

    public string TargetObject { get; set; } = null!;

    public virtual User? User { get; set; }
}
