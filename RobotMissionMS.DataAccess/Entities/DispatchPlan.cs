namespace RobotMissionMS.DataAccess.Entities;

public partial class DispatchPlan
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? DiscardedAt { get; set; }
    public string? Reason { get; set; }
    public int ManualEditCount { get; set; }
    public decimal? EstimatedDistanceM { get; set; }
    public decimal? BaselineDistanceM { get; set; }
    public int? EstimatedDurationSec { get; set; }
    public int? BaselineDurationSec { get; set; }
    public long Version { get; set; }

    public virtual User? CreatedByUser { get; set; }
    public virtual User? ReleasedByUser { get; set; }
    public virtual Zone Zone { get; set; } = null!;
    public virtual ICollection<DispatchPlanTrip> Trips { get; set; } = new List<DispatchPlanTrip>();
    public virtual ICollection<DispatchPlanTask> Tasks { get; set; } = new List<DispatchPlanTask>();
}
