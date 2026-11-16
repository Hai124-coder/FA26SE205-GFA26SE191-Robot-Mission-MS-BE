using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.DataAccess.Persistence;

public partial class RobotMissionDbContext
{
    public virtual DbSet<DispatchPlan> DispatchPlans { get; set; }
    public virtual DbSet<DispatchPlanTrip> DispatchPlanTrips { get; set; }
    public virtual DbSet<DispatchPlanStop> DispatchPlanStops { get; set; }
    public virtual DbSet<DispatchPlanTask> DispatchPlanTasks { get; set; }
}
