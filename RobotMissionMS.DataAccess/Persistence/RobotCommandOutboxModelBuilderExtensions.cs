using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.DataAccess.Persistence;

internal static class RobotCommandOutboxModelBuilderExtensions
{
    public static void ConfigureRobotCommandOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RobotCommandOutbox>(entity =>
        {
            entity.HasKey(value => value.Id).HasName("RobotCommandOutbox_pkey");
            entity.ToTable("RobotCommandOutbox", table =>
            {
                table.HasCheckConstraint("RobotCommandOutbox_CommandType_check", "\"CommandType\" = 'DISPATCH_TRIP'");
                table.HasCheckConstraint("RobotCommandOutbox_AttemptCount_check", "\"AttemptCount\" >= 0");
            });
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.CommandType).HasMaxLength(50);
            entity.Property(value => value.Topic).HasMaxLength(200);
            entity.Property(value => value.PayloadJson).HasColumnType("jsonb");
            entity.Property(value => value.LockedBy).HasMaxLength(100);
            entity.Property(value => value.LastError).HasMaxLength(2000);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.NextAttemptAt).HasDefaultValueSql("now()");
            entity.HasIndex(value => new { value.TripId, value.CommandType }, "RobotCommandOutbox_TripId_CommandType_key").IsUnique();
            entity.HasIndex(value => new { value.Status, value.NextAttemptAt }, "RobotCommandOutbox_Status_NextAttemptAt_idx");
            entity.HasIndex(value => new { value.RobotId, value.CreatedAt }, "RobotCommandOutbox_RobotId_CreatedAt_idx");
            entity.HasOne(value => value.Robot).WithMany().HasForeignKey(value => value.RobotId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("RobotCommandOutbox_RobotId_fkey");
            entity.HasOne(value => value.Trip).WithMany().HasForeignKey(value => value.TripId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("RobotCommandOutbox_TripId_fkey");
            entity.HasOne(value => value.Plan).WithMany().HasForeignKey(value => value.PlanId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("RobotCommandOutbox_PlanId_fkey");
        });
    }
}
