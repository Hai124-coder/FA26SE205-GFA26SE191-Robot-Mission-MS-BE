using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Time-series. Store every few seconds (live view reads MQTT directly) and delete rows older than N days.
/// </summary>
public partial class TelemetrySnapshot
{
    public long Id { get; set; }

    public Guid RobotId { get; set; }

    public DateTime Timestamp { get; set; }

    public int BatteryLevel { get; set; }

    public double? CoordX { get; set; }

    public double? CoordY { get; set; }

    public double? Speed { get; set; }

    public virtual Robot Robot { get; set; } = null!;
}
