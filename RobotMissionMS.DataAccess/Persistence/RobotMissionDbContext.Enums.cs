using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Persistence;

public partial class RobotMissionDbContext
{
    // EF Core scaffolding omits native PostgreSQL enum columns.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>().Property(e => e.Category).HasColumnType("alert_category");
        modelBuilder.Entity<Alert>().Property(e => e.Severity).HasColumnType("alert_severity").HasDefaultValueSql("'WARNING'::alert_severity");
        modelBuilder.Entity<Alert>().Property(e => e.Status).HasColumnType("alert_status").HasDefaultValueSql("'OPEN'::alert_status");
        modelBuilder.Entity<Batch>().Property(e => e.Source).HasColumnType("batch_source").HasDefaultValueSql("'IMPORT'::batch_source");
        modelBuilder.Entity<ChargingSession>().Property(e => e.Status).HasColumnType("charging_session_status").HasDefaultValueSql("'CHARGING'::charging_session_status");
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
        modelBuilder.Entity<Trip>().Property(e => e.Status).HasColumnType("trip_status").HasDefaultValueSql("'PLANNED'::trip_status");
        modelBuilder.Entity<TripStop>().Property(e => e.Action).HasColumnType("trip_stop_action");
        modelBuilder.Entity<TripStop>().Property(e => e.Status).HasColumnType("trip_stop_status").HasDefaultValueSql("'PENDING'::trip_stop_status");
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

