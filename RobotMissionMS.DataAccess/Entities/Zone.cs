using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Each Zone represents one hospital floor. Map size lives in MapVersions.
/// </summary>
public partial class Zone
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();

    /// <summary>All imported map versions for this floor; at most one is PUBLISHED.</summary>
    public virtual ICollection<MapVersion> MapVersions { get; set; } = new List<MapVersion>();

    public virtual ICollection<MissionSchedule> MissionSchedules { get; set; } = new List<MissionSchedule>();

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual ICollection<Robot> Robots { get; set; } = new List<Robot>();

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
