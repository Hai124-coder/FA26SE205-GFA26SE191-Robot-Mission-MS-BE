using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// One PUBLISHED version per zone (partial unique index, see project note).
/// </summary>
public partial class MapVersion
{
    public Guid Id { get; set; }

    public Guid ZoneId { get; set; }

    public int VersionNo { get; set; }

    /// <summary>
    /// map package as imported (nodes, edges, floor size)
    /// </summary>
    public string SourceJson { get; set; } = null!;

    /// <summary>
    /// floor plan PNG, for display only
    /// </summary>
    public string? BackgroundImagePath { get; set; }

    public double WidthM { get; set; }

    public double HeightM { get; set; }

    public Guid? ImportedByUserId { get; set; }

    public DateTime ImportedAt { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public virtual User? ImportedByUser { get; set; }

    public virtual ICollection<MapEdge> MapEdges { get; set; } = new List<MapEdge>();

    public virtual ICollection<MapNode> MapNodes { get; set; } = new List<MapNode>();

    public virtual User? PublishedByUser { get; set; }

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public virtual Zone Zone { get; set; } = null!;
}
