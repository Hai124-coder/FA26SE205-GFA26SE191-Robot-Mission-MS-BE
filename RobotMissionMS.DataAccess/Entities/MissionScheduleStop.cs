using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Points the robot visits on each scheduled run.
/// </summary>
public partial class MissionScheduleStop
{
    public Guid Id { get; set; }

    public Guid ScheduleId { get; set; }

    public int Seq { get; set; }

    public Guid LocationId { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual MissionSchedule Schedule { get; set; } = null!;
}
