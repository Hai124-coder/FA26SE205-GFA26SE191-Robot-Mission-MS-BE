using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

public partial class MapEdge
{
    public Guid Id { get; set; }

    public Guid MapVersionId { get; set; }

    public Guid FromNodeId { get; set; }

    public Guid ToNodeId { get; set; }

    public double DistanceMeters { get; set; }

    public bool IsBidirectional { get; set; }

    /// <summary>
    /// admin temporary lock
    /// </summary>
    public bool IsBlocked { get; set; }

    public DateTime? BlockedAt { get; set; }

    public Guid? BlockedByUserId { get; set; }

    public string? BlockReason { get; set; }

    public virtual User? BlockedByUser { get; set; }

    public virtual MapNode FromNode { get; set; } = null!;

    public virtual MapVersion MapVersion { get; set; } = null!;

    public virtual MapNode ToNode { get; set; } = null!;
}
