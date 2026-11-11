using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Ordered stops of a trip (delivery order from AI Scheduler).
/// </summary>
public partial class TripStop
{
    public Guid Id { get; set; }

    public Guid TripId { get; set; }

    public int Seq { get; set; }

    public Guid LocationId { get; set; }

    public DateTime? ArrivedAt { get; set; }

    public DateTime? DepartedAt { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual Trip Trip { get; set; } = null!;
}
