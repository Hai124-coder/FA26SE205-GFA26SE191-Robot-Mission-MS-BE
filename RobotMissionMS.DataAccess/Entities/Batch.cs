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

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
}
