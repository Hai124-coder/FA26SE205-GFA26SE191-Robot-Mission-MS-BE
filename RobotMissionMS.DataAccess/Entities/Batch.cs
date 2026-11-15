using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Import lot only (CSV/XLSX). Trips are in Trips.
/// </summary>
public partial class Batch
{
    public Guid Id { get; set; }

    public string BatchCode { get; set; } = null!;

    public string? FileName { get; set; }

    public int TotalRows { get; set; }

    public int AcceptedRows { get; set; }

    public int RejectedRows { get; set; }

    /// <summary>
    /// one row can be split into several missions
    /// </summary>
    public int GeneratedMissions { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? TemplateVersion { get; set; }

    public DateTime? CommittedAt { get; set; }

    public Guid? CommittedByUserId { get; set; }

    public DateTime? DiscardedAt { get; set; }

    public Guid? DiscardedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual ICollection<BatchRow> BatchRows { get; set; } = new List<BatchRow>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual User? CommittedByUser { get; set; }

    public virtual User? DiscardedByUser { get; set; }
}
