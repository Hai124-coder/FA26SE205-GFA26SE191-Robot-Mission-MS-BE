using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

public partial class MapNode
{
    public Guid Id { get; set; }

    public Guid MapVersionId { get; set; }

    /// <summary>
    /// stable across versions, e.g. N03
    /// </summary>
    public string Code { get; set; } = null!;

    public double CoordX { get; set; }

    public double CoordY { get; set; }

    public virtual ICollection<MapEdge> MapEdgeFromNodes { get; set; } = new List<MapEdge>();

    public virtual ICollection<MapEdge> MapEdgeToNodes { get; set; } = new List<MapEdge>();

    public virtual MapVersion MapVersion { get; set; } = null!;
}
