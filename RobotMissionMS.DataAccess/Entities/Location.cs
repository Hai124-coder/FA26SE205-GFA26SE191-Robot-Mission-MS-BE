using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Admin assigns node -&gt; department/room. Position comes from the node.
/// </summary>
public partial class Location
{
    public Guid Id { get; set; }

    public Guid ZoneId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// node code in the PUBLISHED map; kept when a new version is published
    /// </summary>
    public string MapNodeCode { get; set; } = null!;

    public bool IsRestrictedArea { get; set; }

    public string? RestrictedHours { get; set; }

    public bool IsActive { get; set; }

    public virtual ChargingStation? ChargingStation { get; set; }

    public virtual ICollection<Mission> MissionFromLocations { get; set; } = new List<Mission>();

    public virtual ICollection<MissionScheduleStop> MissionScheduleStops { get; set; } = new List<MissionScheduleStop>();

    public virtual ICollection<Mission> MissionToLocations { get; set; } = new List<Mission>();

    public virtual ICollection<MissionTypePickupLocation> MissionTypePickupLocations { get; set; } = new List<MissionTypePickupLocation>();

    public virtual ICollection<TripStop> TripStops { get; set; } = new List<TripStop>();

    public virtual Zone Zone { get; set; } = null!;
}
