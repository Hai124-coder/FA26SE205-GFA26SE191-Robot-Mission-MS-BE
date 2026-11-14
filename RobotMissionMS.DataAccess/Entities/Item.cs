using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Item catalogue managed by Admin. Items not in the catalogue cannot be ordered.
/// </summary>
public partial class Item
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// box, bag, piece...
    /// </summary>
    public string Unit { get; set; } = null!;

    /// <summary>
    /// which mission type (robot type) carries this item
    /// </summary>
    public Guid MissionTypeId { get; set; }

    /// <summary>
    /// max quantity of this item in one slot
    /// </summary>
    public int MaxQtyPerSlot { get; set; }

    public bool IsActive { get; set; }

    public string? Description { get; set; }

    public string? Packaging { get; set; }

    public decimal? KgPerUnit { get; set; }

    public string? CapacityLimitReason { get; set; }

    public virtual MissionType MissionType { get; set; } = null!;

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
}
