using System;
using System.Collections.Generic;

namespace RobotMissionMS.DataAccess.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public Guid RoleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<Alert> AlertAcknowledgedByUsers { get; set; } = new List<Alert>();

    public virtual ICollection<Alert> AlertResolvedByUsers { get; set; } = new List<Alert>();

    public virtual ICollection<Batch> Batches { get; set; } = new List<Batch>();

    public virtual ICollection<MapEdge> MapEdges { get; set; } = new List<MapEdge>();

    public virtual ICollection<MapVersion> MapVersionImportedByUsers { get; set; } = new List<MapVersion>();

    public virtual ICollection<MapVersion> MapVersionPublishedByUsers { get; set; } = new List<MapVersion>();

    public virtual ICollection<MissionSchedule> MissionSchedules { get; set; } = new List<MissionSchedule>();

    public virtual ICollection<MissionStatusHistory> MissionStatusHistories { get; set; } = new List<MissionStatusHistory>();

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
