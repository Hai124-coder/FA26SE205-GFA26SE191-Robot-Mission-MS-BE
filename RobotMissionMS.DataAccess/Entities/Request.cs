namespace RobotMissionMS.DataAccess.Entities;

public partial class Request
{
    public Guid Id { get; set; }
    public string RequestCode { get; set; } = null!;
    public Guid ZoneId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
    public virtual Zone Zone { get; set; } = null!;
    public virtual Batch? Batch { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
}
