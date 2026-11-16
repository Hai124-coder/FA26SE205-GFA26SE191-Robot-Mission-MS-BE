namespace RobotMissionMS.DataAccess.Entities;

public partial class DispatchPlanStop
{
    public Guid Id { get; set; }
    public Guid PlanTripId { get; set; }
    public int Seq { get; set; }
    public Guid LocationId { get; set; }
    public DateTime? EstimatedArrivedAt { get; set; }
    public DateTime? EstimatedDepartedAt { get; set; }
    public decimal? CumulativeDistanceM { get; set; }

    public virtual DispatchPlanTrip PlanTrip { get; set; } = null!;
    public virtual Location Location { get; set; } = null!;
    public virtual ICollection<DispatchPlanTask> DeliveryTasks { get; set; } = new List<DispatchPlanTask>();
}
