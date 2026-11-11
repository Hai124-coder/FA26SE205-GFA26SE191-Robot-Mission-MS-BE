using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// A robot belongs to one floor. State = what the robot is doing; IsConnected = network only.
/// </summary>
public partial class Robot
{
    public Guid Id { get; set; }

    public string RobotCode { get; set; } = null!;

    public Guid RobotTypeId { get; set; }

    public Guid ZoneId { get; set; }

    public Guid? HomeChargingStationId { get; set; }

    /// <summary>
    /// from heartbeat / MQTT last will
    /// </summary>
    public bool IsConnected { get; set; }

    public int Battery { get; set; }

    public DateTime? LastHeartbeatAt { get; set; }

    public double? CurrentX { get; set; }

    public double? CurrentY { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual ChargingSession? ChargingSession { get; set; }

    public virtual ChargingStation? HomeChargingStation { get; set; }

    public virtual RobotType RobotType { get; set; } = null!;

    public virtual ICollection<TelemetrySnapshot> TelemetrySnapshots { get; set; } = new List<TelemetrySnapshot>();

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public virtual Zone Zone { get; set; } = null!;
}
