using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Free slots = Capacity - active ChargingSessions.
/// </summary>
public partial class ChargingStation
{
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }

    public string Name { get; set; } = null!;

    public string StationType { get; set; } = null!;

    public int Capacity { get; set; }

    public virtual ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();

    public virtual Location Location { get; set; } = null!;

    public virtual ICollection<Robot> Robots { get; set; } = new List<Robot>();
}
