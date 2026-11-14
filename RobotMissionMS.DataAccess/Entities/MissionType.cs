using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

public partial class MissionType
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public Guid RobotTypeId { get; set; }

    /// <summary>
    /// WF-05 base mission priority
    /// </summary>
    public int BasePriority { get; set; }

    /// <summary>
    /// false for WASTE_CART, CLEAN_BOT
    /// </summary>
    public bool UsesSlotSensor { get; set; }

    /// <summary>
    /// wait after sensor condition is met (placeholder default)
    /// </summary>
    public int DepartDelaySec { get; set; }

    /// <summary>
    /// alert Operator after this (placeholder default)
    /// </summary>
    public int WaitTimeoutSec { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual ICollection<MissionSchedule> MissionSchedules { get; set; } = new List<MissionSchedule>();

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual ICollection<MissionTypePickupLocation> MissionTypePickupLocations { get; set; } = new List<MissionTypePickupLocation>();

    public virtual RobotType RobotType { get; set; } = null!;
}
