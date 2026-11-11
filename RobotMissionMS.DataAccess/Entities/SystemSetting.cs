using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// System config uploaded by Admin (UC08): battery thresholds, AI timeout, priority weights...
/// </summary>
public partial class SystemSetting
{
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public virtual User? UpdatedByUser { get; set; }
}
