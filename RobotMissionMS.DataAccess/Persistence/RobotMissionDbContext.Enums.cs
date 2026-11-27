using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Persistence;

public partial class RobotMissionDbContext
{
    // EF Core scaffolding omits native PostgreSQL enum columns.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureDispatchPlans();
        modelBuilder.ConfigureRobotCommandOutbox();
        modelBuilder.ConfigureIncidentWorkflow();
        modelBuilder.Entity<Alert>().Property(e => e.Category).HasColumnType("alert_category");
        modelBuilder.Entity<Alert>().Property(e => e.Severity).HasColumnType("alert_severity").HasDefaultValueSql("'WARNING'::alert_severity")
            .HasSentinel((AlertSeverity)(-1));
        modelBuilder.Entity<Alert>().Property(e => e.Status).HasColumnType("alert_status").HasDefaultValueSql("'OPEN'::alert_status")
            .HasSentinel((AlertStatus)(-1));
        modelBuilder.Entity<Batch>().Property(e => e.Source).HasColumnType("batch_source").HasDefaultValueSql("'IMPORT'::batch_source");
        modelBuilder.Entity<Batch>().Property(e => e.Status).HasColumnType("batch_status").HasDefaultValueSql("'DRAFT'::batch_status");
        modelBuilder.Entity<Request>().Property(e => e.Source).HasColumnType("batch_source");
        modelBuilder.Entity<BatchRow>().Property(e => e.Priority).HasColumnType("mission_priority");
        modelBuilder.Entity<ChargingSession>().Property(e => e.Status).HasColumnType("charging_session_status").HasDefaultValueSql("'QUEUED'::charging_session_status");
        modelBuilder.Entity<DispatchPlan>().Property(e => e.Status).HasColumnType("dispatch_plan_status").HasDefaultValueSql("'DRAFT'::dispatch_plan_status");
        modelBuilder.Entity<DispatchPlan>().Property(e => e.Source).HasColumnType("dispatch_plan_source");
        modelBuilder.Entity<DispatchPlanStop>().Property(e => e.Action).HasColumnType("trip_stop_action");
        modelBuilder.Entity<DispatchPlanTask>().Property(e => e.Status).HasColumnType("dispatch_plan_task_status");
        modelBuilder.Entity<DispatchPlanTask>().Property(e => e.Priority).HasColumnType("mission_priority");
        modelBuilder.Entity<ChargingStation>().Property(e => e.Status).HasColumnType("station_status").HasDefaultValueSql("'OPERATIONAL'::station_status");
        // Pharmacy is enum value 0: do not treat it as unset and replace it with DEPT.
        modelBuilder.Entity<Location>().Property(e => e.Kind).HasColumnType("location_kind").HasDefaultValueSql("'DEPT'::location_kind")
            .HasSentinel((LocationKind)(-1));
        modelBuilder.Entity<MapNode>().Property(e => e.NodeType).HasColumnType("node_type");
        modelBuilder.Entity<MapVersion>().Property(e => e.Status).HasColumnType("map_version_status").HasDefaultValueSql("'DRAFT'::map_version_status");
        modelBuilder.Entity<Mission>().Property(e => e.Priority).HasColumnType("mission_priority").HasDefaultValueSql("'NORMAL'::mission_priority");
        modelBuilder.Entity<Mission>().Property(e => e.Status).HasColumnType("mission_status").HasDefaultValueSql("'PENDING'::mission_status");
        modelBuilder.Entity<Mission>().Property(e => e.FailReason).HasColumnType("mission_fail_reason");
        modelBuilder.Entity<MissionStatusHistory>().Property(e => e.FromStatus).HasColumnType("mission_status");
        modelBuilder.Entity<MissionStatusHistory>().Property(e => e.ToStatus).HasColumnType("mission_status");
        modelBuilder.Entity<MissionType>().Property(e => e.Mode).HasColumnType("mission_mode");
        modelBuilder.Entity<MissionType>().Property(e => e.PickupKind).HasColumnType("location_kind");
        modelBuilder.Entity<Robot>().Property(e => e.State).HasColumnType("robot_state").HasDefaultValueSql("'INACTIVE'::robot_state");
        modelBuilder.Entity<TelemetrySnapshot>().Property(e => e.State).HasColumnType("robot_state");
        modelBuilder.Entity<MissionScheduleRun>().Property(e => e.Status).HasColumnType("schedule_run_status");
        modelBuilder.Entity<MissionScheduleRun>().Property(e => e.RouteSnapshot).HasColumnType("jsonb");
        modelBuilder.Entity<MissionScheduleRun>(entity =>
        {
            entity.ToTable("MissionScheduleRuns");
            entity.HasKey(e => e.Id).HasName("MissionScheduleRuns_pkey");
            entity.Property(e => e.RouteSnapshot).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.Status, e.ScheduledFor }, "MissionScheduleRuns_Status_ScheduledFor_idx");
            entity.HasIndex(e => new { e.ScheduleId, e.CreatedAt }, "MissionScheduleRuns_ScheduleId_CreatedAt_idx");
            entity.HasOne(e => e.Schedule).WithMany(e => e.MissionScheduleRuns).HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RequestedByUser).WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Mission).WithMany().HasForeignKey(e => e.MissionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LateAlert).WithMany().HasForeignKey(e => e.LateAlertId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ChargingSession>().Property(e => e.QueuedAt).HasColumnType("timestamp with time zone");
        modelBuilder.Entity<MissionSchedule>().Property(e => e.DeletedAt).HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Trip>().Property(e => e.Status).HasColumnType("trip_status").HasDefaultValueSql("'PLANNED'::trip_status");
        modelBuilder.Entity<TripStop>().Property(e => e.Action).HasColumnType("trip_stop_action");
        modelBuilder.Entity<TripStop>().Property(e => e.Status).HasColumnType("trip_stop_status").HasDefaultValueSql("'PENDING'::trip_stop_status");
        modelBuilder.Entity<PasswordResetRequest>().Property(e => e.Status).HasColumnType("password_reset_request_status").HasDefaultValueSql("'OPEN'::password_reset_request_status");
        modelBuilder.Entity<Zone>().Property(e => e.OperationMode).HasColumnType("zone_mode").HasDefaultValueSql("'NORMAL'::zone_mode");
        modelBuilder.Entity<ChargingSession>().HasIndex(e => new { e.ChargingStationId, e.Status }, "ChargingSessions_ChargingStationId_Status_idx");
        modelBuilder.Entity<ChargingSession>().HasIndex(e => new { e.RobotId, e.Status }, "ChargingSessions_RobotId_Status_idx");
        modelBuilder.Entity<Trip>().HasIndex(e => new { e.RobotId, e.Status }, "Trips_RobotId_Status_idx");
        modelBuilder.Entity<Trip>().HasIndex(e => new { e.ZoneId, e.Status }, "Trips_ZoneId_Status_idx");
        modelBuilder.Entity<Alert>().HasIndex(e => new { e.Category, e.Status }, "Alerts_Category_Status_idx");
        modelBuilder.Entity<Robot>().HasIndex(e => new { e.ZoneId, e.State }, "Robots_ZoneId_State_idx");
        modelBuilder.Entity<Mission>().HasIndex(e => new { e.ZoneId, e.Status }, "Missions_ZoneId_Status_idx");
    }
}

