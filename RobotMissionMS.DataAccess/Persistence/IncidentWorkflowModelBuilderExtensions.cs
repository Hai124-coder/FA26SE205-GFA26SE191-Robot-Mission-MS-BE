using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.DataAccess.Persistence;

internal static class IncidentWorkflowModelBuilderExtensions
{
    public static void ConfigureIncidentWorkflow(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("Alerts", table =>
            {
                table.HasCheckConstraint("Alerts_ScenarioCode_check", "\"ScenarioCode\" IS NULL OR length(btrim(\"ScenarioCode\")) > 0");
                table.HasCheckConstraint("Alerts_AutoResolvedAt_check", "\"AutoResolvedAt\" IS NULL OR \"Status\" = 'RESOLVED'::alert_status");
                table.HasCheckConstraint("Alerts_DonePhase_check", "\"Phase\" IS NULL OR \"Phase\" <> 'DONE'::incident_phase OR \"Status\" = 'RESOLVED'::alert_status");
            });
            entity.Property(e => e.ScenarioCode).HasMaxLength(20);
            entity.Property(e => e.Phase).HasColumnType("incident_phase");
            entity.Property(e => e.CargoState).HasColumnType("incident_cargo_state");
            entity.Property(e => e.SuggestedAction).HasColumnType("incident_action");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "Alerts_Status_CreatedAt_idx");
            entity.HasIndex(e => new { e.ZoneId, e.Status, e.CreatedAt }, "Alerts_ZoneId_Status_CreatedAt_idx");
            entity.HasIndex(e => new { e.TripId, e.Status }, "Alerts_TripId_Status_idx");
            entity.HasIndex(e => new { e.ScenarioCode, e.Status }, "Alerts_ScenarioCode_Status_idx");
        });

        modelBuilder.Entity<AlertTimelineEntry>(entity =>
        {
            entity.ToTable("AlertTimelineEntries");
            entity.HasKey(e => e.Id).HasName("AlertTimelineEntries_pkey");
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.EventType).HasColumnType("incident_timeline_event_type");
            entity.Property(e => e.Action).HasColumnType("incident_action");
            entity.Property(e => e.FromPhase).HasColumnType("incident_phase");
            entity.Property(e => e.ToPhase).HasColumnType("incident_phase");
            entity.Property(e => e.Note).HasMaxLength(2000);
            entity.Property(e => e.PayloadJson).HasColumnType("jsonb");
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("AlertTimelineEntries_ActionRequested_check", "\"EventType\" <> 'ACTION_REQUESTED'::incident_timeline_event_type OR \"Action\" IS NOT NULL");
                table.HasCheckConstraint("AlertTimelineEntries_PhaseChanged_check", "\"EventType\" <> 'PHASE_CHANGED'::incident_timeline_event_type OR \"ToPhase\" IS NOT NULL");
                table.HasCheckConstraint("AlertTimelineEntries_PayloadJson_check", "\"PayloadJson\" IS NULL OR jsonb_typeof(\"PayloadJson\") = 'object'");
            });
            entity.HasIndex(e => new { e.AlertId, e.OccurredAt, e.Id }, "AlertTimelineEntries_AlertId_OccurredAt_Id_idx");
            entity.HasIndex(e => new { e.ActorUserId, e.OccurredAt }, "AlertTimelineEntries_ActorUserId_OccurredAt_idx");
            entity.HasOne(e => e.Alert).WithMany(e => e.TimelineEntries)
                .HasForeignKey(e => e.AlertId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AlertTimelineEntries_AlertId_fkey");
            entity.HasOne(e => e.ActorUser).WithMany(e => e.AlertTimelineEntries)
                .HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AlertTimelineEntries_ActorUserId_fkey");
        });

        modelBuilder.Entity<AlertMission>(entity =>
        {
            entity.ToTable("AlertMissions");
            entity.HasKey(e => new { e.AlertId, e.MissionId }).HasName("AlertMissions_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.MissionId, e.CreatedAt }, "AlertMissions_MissionId_CreatedAt_idx");
            entity.HasOne(e => e.Alert).WithMany(e => e.AlertMissions)
                .HasForeignKey(e => e.AlertId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AlertMissions_AlertId_fkey");
            entity.HasOne(e => e.Mission).WithMany(e => e.AlertMissions)
                .HasForeignKey(e => e.MissionId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AlertMissions_MissionId_fkey");
        });
    }
}
