using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence.Configurations;

namespace RobotMissionMS.DataAccess.Persistence;

public partial class RobotMissionDbContext : DbContext
{
    public RobotMissionDbContext(DbContextOptions<RobotMissionDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActivityLog> ActivityLogs { get; set; }

    public virtual DbSet<Alert> Alerts { get; set; }

    public virtual DbSet<Batch> Batches { get; set; }

    public virtual DbSet<ChargingSession> ChargingSessions { get; set; }

    public virtual DbSet<ChargingStation> ChargingStations { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<MapEdge> MapEdges { get; set; }

    public virtual DbSet<MapNode> MapNodes { get; set; }

    public virtual DbSet<MapVersion> MapVersions { get; set; }

    public virtual DbSet<Mission> Missions { get; set; }

    public virtual DbSet<MissionSchedule> MissionSchedules { get; set; }

    public virtual DbSet<MissionScheduleStop> MissionScheduleStops { get; set; }

    public virtual DbSet<MissionStatusHistory> MissionStatusHistories { get; set; }

    public virtual DbSet<MissionType> MissionTypes { get; set; }

    public virtual DbSet<Robot> Robots { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<RobotType> RobotTypes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<TelemetrySnapshot> TelemetrySnapshots { get; set; }

    public virtual DbSet<Trip> Trips { get; set; }

    public virtual DbSet<TripStop> TripStops { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Zone> Zones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("alert_category", new[] { "ROBOT", "MISSION", "SYSTEM" })
            .HasPostgresEnum("alert_severity", new[] { "INFO", "WARNING", "CRITICAL" })
            .HasPostgresEnum("alert_status", new[] { "OPEN", "ACKNOWLEDGED", "RESOLVED" })
            .HasPostgresEnum("batch_source", new[] { "IMPORT", "MANUAL" })
            .HasPostgresEnum("charging_session_status", new[] { "CHARGING", "COMPLETED", "INTERRUPTED" })
            .HasPostgresEnum("location_kind", new[] { "PHARMACY", "STORAGE", "SPECIMEN", "DEPT", "WASTE_STATION", "CHARGER", "CLEAN_STATION", "WASTE_POINT", "CORRIDOR" })
            .HasPostgresEnum("map_version_status", new[] { "DRAFT", "PUBLISHED", "ARCHIVED" })
            .HasPostgresEnum("mission_fail_reason", new[] { "NO_RECIPIENT", "LOCATION_CLOSED", "OTHER" })
            .HasPostgresEnum("mission_mode", new[] { "BATCH", "SCHEDULE" })
            .HasPostgresEnum("mission_priority", new[] { "NORMAL", "HIGH", "URGENT" })
            .HasPostgresEnum("mission_status", new[] { "PENDING", "ASSIGNED", "IN_PROGRESS", "DELIVERY_FAILED", "COMPLETED", "COMPLETED_BY_HUMAN", "RESCHEDULED", "CANCELLED" })
            .HasPostgresEnum("node_type", new[] { "WAYPOINT", "STOP", "CHARGER" })
            .HasPostgresEnum("robot_state", new[] { "INACTIVE", "AVAILABLE", "ON_TRIP", "CHARGING", "ERROR", "MAINTENANCE" })
            .HasPostgresEnum("station_status", new[] { "OPERATIONAL", "OUT_OF_SERVICE" })
            .HasPostgresEnum("trip_status", new[] { "PLANNED", "DISPATCHED", "IN_PROGRESS", "COMPLETED", "ABORTED" })
            .HasPostgresEnum("trip_stop_action", new[] { "PICKUP", "DROP", "ROUTE_POINT", "RETURN" })
            .HasPostgresEnum("trip_stop_status", new[] { "PENDING", "ARRIVED", "DONE", "SKIPPED" })
            .HasPostgresEnum("zone_mode", new[] { "NORMAL", "DRAINING", "MAINTENANCE" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ActivityLogs_pkey");

            entity.ToTable(tb => tb.HasComment("Stores user activity history, including map imports and order batching (WF-01, WF-02)."));

            entity.HasIndex(e => e.Timestamp, "ActivityLogs_Timestamp_idx");

            entity.HasIndex(e => e.UserId, "ActivityLogs_UserId_idx");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.TargetObject).HasMaxLength(255);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany(p => p.ActivityLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("ActivityLogs_UserId_fkey");
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Alerts_pkey");

            entity.HasIndex(e => e.MissionId, "Alerts_MissionId_idx");

            entity.HasIndex(e => e.RobotId, "Alerts_RobotId_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.AlertType)
                .HasMaxLength(50)
                .HasComment("e.g. ROBOT_OFFLINE, NOT_RECEIVED, LOW_BATTERY, BLOCKED");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.AcknowledgedByUser).WithMany(p => p.AlertAcknowledgedByUsers)
                .HasForeignKey(d => d.AcknowledgedByUserId)
                .HasConstraintName("Alerts_AcknowledgedByUserId_fkey");

            entity.HasOne(d => d.Mission).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.MissionId)
                .HasConstraintName("Alerts_MissionId_fkey");

            entity.HasOne(d => d.ResolvedByUser).WithMany(p => p.AlertResolvedByUsers)
                .HasForeignKey(d => d.ResolvedByUserId)
                .HasConstraintName("Alerts_ResolvedByUserId_fkey");

            entity.HasOne(d => d.Robot).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.RobotId)
                .HasConstraintName("Alerts_RobotId_fkey");

            entity.HasOne(d => d.Trip).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.TripId)
                .HasConstraintName("Alerts_TripId_fkey");

            entity.HasOne(d => d.Zone).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.ZoneId)
                .HasConstraintName("Alerts_ZoneId_fkey");
        });

        modelBuilder.Entity<Batch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Batches_pkey");

            entity.ToTable(tb => tb.HasComment("Import lot only (CSV/XLSX). Trips are in Trips."));

            entity.HasIndex(e => e.BatchCode, "Batches_BatchCode_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.AcceptedRows).HasDefaultValue(0);
            entity.Property(e => e.BatchCode).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.GeneratedMissions)
                .HasDefaultValue(0)
                .HasComment("one row can be split into several missions");
            entity.Property(e => e.RejectedRows).HasDefaultValue(0);
            entity.Property(e => e.TotalRows).HasDefaultValue(0);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Batches)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("Batches_CreatedByUserId_fkey");
        });

        modelBuilder.Entity<ChargingSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ChargingSessions_pkey");

            entity.ToTable(tb => tb.HasComment("Which robot is charging at which station, plus charging history. One active session per robot."));

            entity.HasIndex(e => e.RobotId, "UX_ChargingSessions_OneActive")
                .IsUnique()
                .HasFilter("(\"Status\" = 'CHARGING'::charging_session_status)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ChargingStation).WithMany(p => p.ChargingSessions)
                .HasForeignKey(d => d.ChargingStationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ChargingSessions_ChargingStationId_fkey");

            entity.HasOne(d => d.Robot).WithOne(p => p.ChargingSession)
                .HasForeignKey<ChargingSession>(d => d.RobotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ChargingSessions_RobotId_fkey");
        });

        modelBuilder.Entity<ChargingStation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ChargingStations_pkey");

            entity.ToTable(tb => tb.HasComment("Free slots = Capacity - active ChargingSessions."));

            entity.HasIndex(e => e.LocationId, "ChargingStations_LocationId_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Capacity).HasDefaultValue(1);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.StationType)
                .HasMaxLength(30)
                .HasDefaultValueSql("'CHARGER'::character varying");

            entity.HasOne(d => d.Location).WithOne(p => p.ChargingStation)
                .HasForeignKey<ChargingStation>(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ChargingStations_LocationId_fkey");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Items_pkey");

            entity.ToTable(tb => tb.HasComment("Item catalogue managed by Admin. Items not in the catalogue cannot be ordered."));

            entity.HasIndex(e => e.Code, "Items_Code_key").IsUnique();

            entity.HasIndex(e => e.MissionTypeId, "Items_MissionTypeId_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxQtyPerSlot).HasComment("max quantity of this item in one slot");
            entity.Property(e => e.MissionTypeId).HasComment("which mission type (robot type) carries this item");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Unit)
                .HasMaxLength(30)
                .HasComment("box, bag, piece...");

            entity.HasOne(d => d.MissionType).WithMany(p => p.Items)
                .HasForeignKey(d => d.MissionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Items_MissionTypeId_fkey");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Locations_pkey");

            entity.ToTable(tb => tb.HasComment("Admin assigns node -> department/room. Position comes from the node."));

            entity.HasIndex(e => new { e.ZoneId, e.Code }, "Locations_ZoneId_Code_idx").IsUnique();

            entity.HasIndex(e => new { e.ZoneId, e.MapNodeCode }, "Locations_ZoneId_MapNodeCode_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsRestrictedArea).HasDefaultValue(false);
            entity.Property(e => e.MapNodeCode)
                .HasMaxLength(50)
                .HasComment("node code in the PUBLISHED map; kept when a new version is published");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.RestrictedHours).HasMaxLength(50);

            entity.HasOne(d => d.Zone).WithMany(p => p.Locations)
                .HasForeignKey(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Locations_ZoneId_fkey");
        });

        modelBuilder.Entity<MapEdge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MapEdges_pkey");

            entity.HasIndex(e => new { e.MapVersionId, e.FromNodeId, e.ToNodeId }, "MapEdges_MapVersionId_FromNodeId_ToNodeId_idx").IsUnique();

            entity.HasIndex(e => e.ToNodeId, "MapEdges_ToNodeId_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.IsBidirectional).HasDefaultValue(true);
            entity.Property(e => e.IsBlocked)
                .HasDefaultValue(false)
                .HasComment("admin temporary lock");

            entity.HasOne(d => d.BlockedByUser).WithMany(p => p.MapEdges)
                .HasForeignKey(d => d.BlockedByUserId)
                .HasConstraintName("MapEdges_BlockedByUserId_fkey");

            entity.HasOne(d => d.FromNode).WithMany(p => p.MapEdgeFromNodes)
                .HasForeignKey(d => d.FromNodeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MapEdges_FromNodeId_fkey");

            entity.HasOne(d => d.MapVersion).WithMany(p => p.MapEdges)
                .HasForeignKey(d => d.MapVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MapEdges_MapVersionId_fkey");

            entity.HasOne(d => d.ToNode).WithMany(p => p.MapEdgeToNodes)
                .HasForeignKey(d => d.ToNodeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MapEdges_ToNodeId_fkey");
        });

        modelBuilder.Entity<MapNode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MapNodes_pkey");

            entity.HasIndex(e => new { e.MapVersionId, e.Code }, "MapNodes_MapVersionId_Code_idx").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasComment("stable across versions, e.g. N03");

            entity.HasOne(d => d.MapVersion).WithMany(p => p.MapNodes)
                .HasForeignKey(d => d.MapVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MapNodes_MapVersionId_fkey");
        });

        modelBuilder.Entity<MapVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MapVersions_pkey");

            entity.ToTable(tb => tb.HasComment("One PUBLISHED version per zone (partial unique index, see project note)."));

            entity.HasIndex(e => new { e.ZoneId, e.VersionNo }, "MapVersions_ZoneId_VersionNo_idx").IsUnique();

            entity.HasIndex(e => e.ZoneId, "UX_MapVersions_OnePublished")
                .IsUnique()
                .HasFilter("(\"Status\" = 'PUBLISHED'::map_version_status)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.BackgroundImagePath)
                .HasMaxLength(500)
                .HasComment("floor plan PNG, for display only");
            entity.Property(e => e.ImportedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.SourceJson)
                .HasComment("map package as imported (nodes, edges, floor size)")
                .HasColumnType("jsonb");

            entity.HasOne(d => d.ImportedByUser).WithMany(p => p.MapVersionImportedByUsers)
                .HasForeignKey(d => d.ImportedByUserId)
                .HasConstraintName("MapVersions_ImportedByUserId_fkey");

            entity.HasOne(d => d.PublishedByUser).WithMany(p => p.MapVersionPublishedByUsers)
                .HasForeignKey(d => d.PublishedByUserId)
                .HasConstraintName("MapVersions_PublishedByUserId_fkey");

            entity.HasOne(d => d.Zone).WithOne(p => p.MapVersion)
                .HasForeignKey<MapVersion>(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MapVersions_ZoneId_fkey");
        });

        modelBuilder.Entity<Mission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Missions_pkey");

            entity.ToTable(tb => tb.HasComment("Robot and both locations must belong to the same Zone as the mission."));

            entity.HasIndex(e => e.BatchId, "Missions_BatchId_idx");

            entity.HasIndex(e => e.MissionCode, "Missions_MissionCode_key").IsUnique();

            entity.HasIndex(e => e.RequestCode, "Missions_RequestCode_idx");

            entity.HasIndex(e => new { e.ScheduleId, e.ScheduledFor }, "Missions_ScheduleId_ScheduledFor_idx").IsUnique();

            entity.HasIndex(e => e.TripId, "Missions_TripId_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FromLocationId).HasComment("required unless ScheduleId is set");
            entity.Property(e => e.ItemId).HasComment("one item type per mission; required unless ScheduleId is set");
            entity.Property(e => e.MissionCode).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasComment("1..Items.MaxQtyPerSlot (checked in backend)");
            entity.Property(e => e.RequestCode)
                .HasMaxLength(50)
                .HasComment("same code for missions split from one request / import row");
            entity.Property(e => e.ScheduleId).HasComment("set for missions generated by a recurring schedule");
            entity.Property(e => e.ScheduledFor).HasComment("schedule run time this mission was generated for");
            entity.Property(e => e.SlotNo).HasComment("slot on the robot (one mission per slot)");
            entity.Property(e => e.ToLocationId).HasComment("required unless ScheduleId is set");
            entity.Property(e => e.TripId).HasComment("current trip; the robot is on the trip");

            entity.HasOne(d => d.Batch).WithMany(p => p.Missions)
                .HasForeignKey(d => d.BatchId)
                .HasConstraintName("Missions_BatchId_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Missions)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("Missions_CreatedByUserId_fkey");

            entity.HasOne(d => d.FromLocation).WithMany(p => p.MissionFromLocations)
                .HasForeignKey(d => d.FromLocationId)
                .HasConstraintName("Missions_FromLocationId_fkey");

            entity.HasOne(d => d.Item).WithMany(p => p.Missions)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("Missions_ItemId_fkey");

            entity.HasOne(d => d.MissionType).WithMany(p => p.Missions)
                .HasForeignKey(d => d.MissionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Missions_MissionTypeId_fkey");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Missions)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("Missions_ScheduleId_fkey");

            entity.HasOne(d => d.ToLocation).WithMany(p => p.MissionToLocations)
                .HasForeignKey(d => d.ToLocationId)
                .HasConstraintName("Missions_ToLocationId_fkey");

            entity.HasOne(d => d.Trip).WithMany(p => p.Missions)
                .HasForeignKey(d => d.TripId)
                .HasConstraintName("Missions_TripId_fkey");

            entity.HasOne(d => d.Zone).WithMany(p => p.Missions)
                .HasForeignKey(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Missions_ZoneId_fkey");
        });

        modelBuilder.Entity<MissionSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MissionSchedules_pkey");

            entity.ToTable(tb => tb.HasComment("Recurring schedules for WASTE_CART and CLEAN_BOT (UC31)."));

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DaysOfWeek).HasComment("1 = Monday ... 7 = Sunday");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.MissionSchedules)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("MissionSchedules_CreatedByUserId_fkey");

            entity.HasOne(d => d.MissionType).WithMany(p => p.MissionSchedules)
                .HasForeignKey(d => d.MissionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MissionSchedules_MissionTypeId_fkey");

            entity.HasOne(d => d.Zone).WithMany(p => p.MissionSchedules)
                .HasForeignKey(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MissionSchedules_ZoneId_fkey");
        });

        modelBuilder.Entity<MissionScheduleStop>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MissionScheduleStops_pkey");

            entity.ToTable(tb => tb.HasComment("Points the robot visits on each scheduled run."));

            entity.HasIndex(e => new { e.ScheduleId, e.Seq }, "MissionScheduleStops_ScheduleId_Seq_idx").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Location).WithMany(p => p.MissionScheduleStops)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MissionScheduleStops_LocationId_fkey");

            entity.HasOne(d => d.Schedule).WithMany(p => p.MissionScheduleStops)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MissionScheduleStops_ScheduleId_fkey");
        });

        modelBuilder.Entity<MissionStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MissionStatusHistory_pkey");

            entity.ToTable("MissionStatusHistory", tb => tb.HasComment("Mission (not robot) state transitions for tracking and audit."));

            entity.HasIndex(e => new { e.MissionId, e.ChangedAt }, "MissionStatusHistory_MissionId_ChangedAt_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.MissionStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("MissionStatusHistory_ChangedByUserId_fkey");

            entity.HasOne(d => d.Mission).WithMany(p => p.MissionStatusHistories)
                .HasForeignKey(d => d.MissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MissionStatusHistory_MissionId_fkey");
        });

        modelBuilder.Entity<MissionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MissionTypes_pkey");

            entity.HasIndex(e => e.Code, "MissionTypes_Code_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.BasePriority)
                .HasDefaultValue(0)
                .HasComment("WF-05 base mission priority");
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.DepartDelaySec)
                .HasDefaultValue(10)
                .HasComment("wait after sensor condition is met (placeholder default)");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.UsesSlotSensor)
                .HasDefaultValue(true)
                .HasComment("false for WASTE_CART, CLEAN_BOT");
            entity.Property(e => e.WaitTimeoutSec)
                .HasDefaultValue(180)
                .HasComment("alert Operator after this (placeholder default)");

            entity.HasOne(d => d.RobotType).WithMany(p => p.MissionTypes)
                .HasForeignKey(d => d.RobotTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MissionTypes_RobotTypeId_fkey");
        });

        modelBuilder.Entity<Robot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Robots_pkey");

            entity.ToTable(tb => tb.HasComment("A robot belongs to one floor. State = what the robot is doing; IsConnected = network only."));

            entity.HasIndex(e => e.RobotCode, "Robots_RobotCode_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Battery).HasDefaultValue(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsConnected)
                .HasDefaultValue(false)
                .HasComment("from heartbeat / MQTT last will");
            entity.Property(e => e.RobotCode).HasMaxLength(50);

            entity.HasOne(d => d.HomeChargingStation).WithMany(p => p.Robots)
                .HasForeignKey(d => d.HomeChargingStationId)
                .HasConstraintName("Robots_HomeChargingStationId_fkey");

            entity.HasOne(d => d.RobotType).WithMany(p => p.Robots)
                .HasForeignKey(d => d.RobotTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Robots_RobotTypeId_fkey");

            entity.HasOne(d => d.Zone).WithMany(p => p.Robots)
                .HasForeignKey(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Robots_ZoneId_fkey");
        });

        modelBuilder.Entity<RobotType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("RobotTypes_pkey");

            entity.HasIndex(e => e.TypeCode, "RobotTypes_TypeCode_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ColorHex).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slots).HasDefaultValue(0);
            entity.Property(e => e.TypeCode).HasMaxLength(50);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Roles_pkey");

            entity.HasIndex(e => e.Name, "Roles_Name_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("SystemSettings_pkey");

            entity.ToTable(tb => tb.HasComment("System config uploaded by Admin (UC08): battery thresholds, AI timeout, priority weights..."));

            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedByUserId)
                .HasConstraintName("SystemSettings_UpdatedByUserId_fkey");
        });

        modelBuilder.Entity<TelemetrySnapshot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TelemetrySnapshots_pkey");

            entity.ToTable(tb => tb.HasComment("Time-series. Store every few seconds (live view reads MQTT directly) and delete rows older than N days."));

            entity.HasIndex(e => new { e.RobotId, e.Timestamp }, "TelemetrySnapshots_RobotId_Timestamp_idx");

            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Robot).WithMany(p => p.TelemetrySnapshots)
                .HasForeignKey(d => d.RobotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TelemetrySnapshots_RobotId_fkey");
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Trips_pkey");

            entity.ToTable(tb => tb.HasComment("One trip = many missions (order batching)."));

            entity.HasIndex(e => e.TripCode, "Trips_TripCode_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DispatchedByUserId).HasComment("null when dispatched automatically");
            entity.Property(e => e.MapVersionId).HasComment("map version used to plan this trip");
            entity.Property(e => e.ReplacesTripId).HasComment("set when this trip takes over an ABORTED trip (WF-04)");
            entity.Property(e => e.TripCode).HasMaxLength(50);

            entity.HasOne(d => d.DispatchedByUser).WithMany(p => p.Trips)
                .HasForeignKey(d => d.DispatchedByUserId)
                .HasConstraintName("Trips_DispatchedByUserId_fkey");

            entity.HasOne(d => d.MapVersion).WithMany(p => p.Trips)
                .HasForeignKey(d => d.MapVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Trips_MapVersionId_fkey");

            entity.HasOne(d => d.ReplacesTrip).WithMany(p => p.InverseReplacesTrip)
                .HasForeignKey(d => d.ReplacesTripId)
                .HasConstraintName("Trips_ReplacesTripId_fkey");

            entity.HasOne(d => d.Robot).WithMany(p => p.Trips)
                .HasForeignKey(d => d.RobotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Trips_RobotId_fkey");

            entity.HasOne(d => d.Zone).WithMany(p => p.Trips)
                .HasForeignKey(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Trips_ZoneId_fkey");
        });

        modelBuilder.Entity<TripStop>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TripStops_pkey");

            entity.ToTable(tb => tb.HasComment("Ordered stops of a trip (delivery order from AI Scheduler)."));

            entity.HasIndex(e => new { e.TripId, e.Seq }, "TripStops_TripId_Seq_idx").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Location).WithMany(p => p.TripStops)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TripStops_LocationId_fkey");

            entity.HasOne(d => d.Trip).WithMany(p => p.TripStops)
                .HasForeignKey(d => d.TripId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TripStops_TripId_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Users_pkey");

            entity.HasIndex(e => e.Username, "Users_Username_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(100);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Users_RoleId_fkey");
        });

        modelBuilder.Entity<Zone>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Zones_pkey");

            entity.ToTable(tb => tb.HasComment("Each Zone represents one hospital floor. Map size lives in MapVersions."));

            entity.HasIndex(e => e.Code, "Zones_Code_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

