using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Recurring schedules for WASTE_CART and CLEAN_BOT (UC31).
/// </summary>
public partial class MissionSchedule
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public Guid MissionTypeId { get; set; }

    public Guid ZoneId { get; set; }

    /// <summary>
    /// 1 = Monday ... 7 = Sunday
    /// </summary>
    public List<int> DaysOfWeek { get; set; } = null!;

    public TimeOnly RunTime { get; set; }

    public bool IsActive { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<MissionScheduleStop> MissionScheduleStops { get; set; } = new List<MissionScheduleStop>();

    public virtual MissionType MissionType { get; set; } = null!;

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual Zone Zone { get; set; } = null!;
}
