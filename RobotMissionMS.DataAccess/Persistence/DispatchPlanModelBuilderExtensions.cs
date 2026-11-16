using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.DataAccess.Persistence;

internal static class DispatchPlanModelBuilderExtensions
{
    public static void ConfigureDispatchPlans(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DispatchPlan>(entity =>
        {
            entity.HasKey(value => value.Id).HasName("DispatchPlans_pkey");
            entity.ToTable("DispatchPlans", table =>
            {
                table.HasCheckConstraint("DispatchPlans_ManualEditCount_check", "\"ManualEditCount\" >= 0");
                table.HasCheckConstraint("DispatchPlans_Version_check", "\"Version\" > 0");
            });
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.Reason).HasMaxLength(1000);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.ManualEditCount).HasDefaultValue(0);
            entity.Property(value => value.EstimatedDistanceM).HasPrecision(12, 2);
            entity.Property(value => value.BaselineDistanceM).HasPrecision(12, 2);
            entity.Property(value => value.Version).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(value => new { value.ZoneId, value.Status, value.CreatedAt }, "DispatchPlans_ZoneId_Status_CreatedAt_idx");
            entity.HasOne(value => value.Zone).WithMany().HasForeignKey(value => value.ZoneId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlans_ZoneId_fkey");
            entity.HasOne(value => value.CreatedByUser).WithMany().HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlans_CreatedByUserId_fkey");
            entity.HasOne(value => value.ReleasedByUser).WithMany().HasForeignKey(value => value.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlans_ReleasedByUserId_fkey");
        });

        modelBuilder.Entity<DispatchPlanTrip>(entity =>
        {
            entity.HasKey(value => value.Id).HasName("DispatchPlanTrips_pkey");
            entity.ToTable("DispatchPlanTrips");
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.EstimatedDistanceM).HasPrecision(12, 2);
            entity.HasIndex(value => new { value.PlanId, value.RobotId }, "DispatchPlanTrips_PlanId_RobotId_key").IsUnique();
            entity.HasIndex(value => new { value.PlanId, value.Seq }, "DispatchPlanTrips_PlanId_Seq_key").IsUnique();
            entity.HasIndex(value => value.RobotId, "DispatchPlanTrips_RobotId_idx");
            entity.HasAlternateKey(value => new { value.Id, value.PlanId }).HasName("DispatchPlanTrips_Id_PlanId_key");
            entity.HasOne(value => value.Plan).WithMany(value => value.Trips).HasForeignKey(value => value.PlanId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTrips_PlanId_fkey");
            entity.HasOne(value => value.Robot).WithMany().HasForeignKey(value => value.RobotId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTrips_RobotId_fkey");
            entity.HasOne(value => value.MapVersion).WithMany().HasForeignKey(value => value.MapVersionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTrips_MapVersionId_fkey");
            entity.HasOne(value => value.PickupLocation).WithMany().HasForeignKey(value => value.PickupLocationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTrips_PickupLocationId_fkey");
        });

        modelBuilder.Entity<DispatchPlanStop>(entity =>
        {
            entity.HasKey(value => value.Id).HasName("DispatchPlanStops_pkey");
            entity.ToTable("DispatchPlanStops");
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.CumulativeDistanceM).HasPrecision(12, 2);
            entity.HasIndex(value => new { value.PlanTripId, value.Seq }, "DispatchPlanStops_PlanTripId_Seq_key").IsUnique();
            entity.HasAlternateKey(value => new { value.Id, value.PlanTripId }).HasName("DispatchPlanStops_Id_PlanTripId_key");
            entity.HasOne(value => value.PlanTrip).WithMany(value => value.Stops).HasForeignKey(value => value.PlanTripId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanStops_PlanTripId_fkey");
            entity.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanStops_LocationId_fkey");
        });

        modelBuilder.Entity<DispatchPlanTask>(entity =>
        {
            entity.HasKey(value => value.Id).HasName("DispatchPlanTasks_pkey");
            entity.ToTable("DispatchPlanTasks");
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.WaitReason).HasMaxLength(500);
            entity.Property(value => value.LateReason).HasMaxLength(500);
            entity.HasIndex(value => new { value.PlanId, value.MissionId }, "DispatchPlanTasks_PlanId_MissionId_key").IsUnique();
            entity.HasIndex(value => new { value.PlanId, value.Status }, "DispatchPlanTasks_PlanId_Status_idx");
            entity.HasIndex(value => value.MissionId, "DispatchPlanTasks_MissionId_idx");
            entity.HasOne(value => value.Plan).WithMany(value => value.Tasks).HasForeignKey(value => value.PlanId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTasks_PlanId_fkey");
            entity.HasOne(value => value.Mission).WithMany().HasForeignKey(value => value.MissionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTasks_MissionId_fkey");
            entity.HasOne(value => value.PlanTrip).WithMany(value => value.Tasks).HasForeignKey(value => new { value.PlanTripId, value.PlanId }).HasPrincipalKey(value => new { value.Id, value.PlanId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTasks_PlanTripId_PlanId_fkey");
            entity.HasOne(value => value.DeliveryPlanStop).WithMany(value => value.DeliveryTasks).HasForeignKey(value => new { value.DeliveryPlanStopId, value.PlanTripId }).HasPrincipalKey(value => new { value.Id, value.PlanTripId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("DispatchPlanTasks_DeliveryPlanStopId_PlanTripId_fkey");
        });
    }
}
