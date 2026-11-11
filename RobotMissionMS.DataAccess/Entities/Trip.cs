using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// One trip = many missions (order batching).
/// </summary>
public partial class Trip
{
    public Guid Id { get; set; }

    public string TripCode { get; set; } = null!;

    public Guid ZoneId { get; set; }

    public Guid RobotId { get; set; }

    /// <summary>
    /// map version used to plan this trip
    /// </summary>
    public Guid MapVersionId { get; set; }

    /// <summary>
    /// set when this trip takes over an ABORTED trip (WF-04)
    /// </summary>
    public Guid? ReplacesTripId { get; set; }

    /// <summary>
    /// null when dispatched automatically
    /// </summary>
    public Guid? DispatchedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual User? DispatchedByUser { get; set; }

    public virtual ICollection<Trip> InverseReplacesTrip { get; set; } = new List<Trip>();

    public virtual MapVersion MapVersion { get; set; } = null!;

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual Trip? ReplacesTrip { get; set; }

    public virtual Robot Robot { get; set; } = null!;

    public virtual ICollection<TripStop> TripStops { get; set; } = new List<TripStop>();

    public virtual Zone Zone { get; set; } = null!;
}
