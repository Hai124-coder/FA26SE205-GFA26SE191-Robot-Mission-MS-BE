using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Which robot is charging at which station, plus charging history. One active session per robot.
/// </summary>
public partial class ChargingSession
{
    public Guid Id { get; set; }

    public Guid RobotId { get; set; }

    public Guid ChargingStationId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual ChargingStation ChargingStation { get; set; } = null!;

    public virtual Robot Robot { get; set; } = null!;
}
