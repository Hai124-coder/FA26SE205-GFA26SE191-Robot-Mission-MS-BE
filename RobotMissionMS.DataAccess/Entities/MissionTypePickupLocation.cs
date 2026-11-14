namespace RobotMissionMS.DataAccess.Entities;

public partial class MissionTypePickupLocation
{
    public Guid Id { get; set; }
    public Guid MissionTypeId { get; set; }
    public Guid ZoneId { get; set; }
    public Guid LocationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public virtual MissionType MissionType { get; set; } = null!;
    public virtual Zone Zone { get; set; } = null!;
    public virtual Location Location { get; set; } = null!;
    public virtual User? UpdatedByUser { get; set; }
}
