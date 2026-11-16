namespace RobotMissionMS.DataAccess.Entities;

public partial class DispatchPlanTrip
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid RobotId { get; set; }
    public Guid MapVersionId { get; set; }
    public Guid? PickupLocationId { get; set; }
    public int Seq { get; set; }
    public DateTime? EstimatedStartAt { get; set; }
    public DateTime? EstimatedEndAt { get; set; }
    public decimal? EstimatedDistanceM { get; set; }

    public virtual DispatchPlan Plan { get; set; } = null!;
    public virtual Robot Robot { get; set; } = null!;
    public virtual MapVersion MapVersion { get; set; } = null!;
    public virtual Location? PickupLocation { get; set; }
    public virtual ICollection<DispatchPlanStop> Stops { get; set; } = new List<DispatchPlanStop>();
    public virtual ICollection<DispatchPlanTask> Tasks { get; set; } = new List<DispatchPlanTask>();
}
