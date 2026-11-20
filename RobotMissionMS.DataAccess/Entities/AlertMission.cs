namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Links an incident to every mission affected by its chosen resolution.
/// </summary>
public class AlertMission
{
    public Guid AlertId { get; set; }
    public Guid MissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Alert Alert { get; set; } = null!;
    public virtual Mission Mission { get; set; } = null!;
}
