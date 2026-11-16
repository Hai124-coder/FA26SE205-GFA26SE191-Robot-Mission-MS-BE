namespace RobotMissionMS.DataAccess.Entities;

public partial class DispatchPlanTask
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid MissionId { get; set; }
    public Guid? PlanTripId { get; set; }
    public Guid? DeliveryPlanStopId { get; set; }
    public int? SlotNo { get; set; }
    public DateTime? EstimatedDeliveryAt { get; set; }
    public string? WaitReason { get; set; }
    public string? LateReason { get; set; }

    public virtual DispatchPlan Plan { get; set; } = null!;
    public virtual Mission Mission { get; set; } = null!;
    public virtual DispatchPlanTrip? PlanTrip { get; set; }
    public virtual DispatchPlanStop? DeliveryPlanStop { get; set; }
}
