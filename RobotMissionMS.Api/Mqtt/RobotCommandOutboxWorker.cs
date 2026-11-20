using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Mqtt;

public sealed class RobotCommandOutboxWorker(IServiceScopeFactory scopes, IRobotCommandPublisher publisher, IOptions<MqttOptions> options, ILogger<RobotCommandOutboxWorker> logger) : BackgroundService
{
    private readonly MqttOptions settings = options.Value;
    private readonly string workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled) { logger.LogInformation("MQTT outbox worker is disabled."); return; }
        if (string.IsNullOrWhiteSpace(settings.Host)) { logger.LogError("MQTT outbox worker is enabled but Mqtt:Host is empty."); return; }
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.PollIntervalSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var index = 0; index < Math.Clamp(settings.BatchSize, 1, 100); index++)
                    if (!await ProcessOneAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "MQTT outbox worker cycle failed."); }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        RobotCommandOutbox? command;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var now = DateTime.UtcNow;
            command = await db.RobotCommandOutbox.FromSqlInterpolated($"""
                SELECT * FROM public."RobotCommandOutbox"
                WHERE ("Status" = 'PENDING'::public.robot_command_outbox_status AND "NextAttemptAt" <= {now})
                   OR ("Status" = 'PROCESSING'::public.robot_command_outbox_status AND "LockedUntil" <= {now})
                ORDER BY "NextAttemptAt", "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """).SingleOrDefaultAsync(cancellationToken);
            if (command is null) { await transaction.CommitAsync(cancellationToken); return false; }
            command.Status = RobotCommandOutboxStatus.Processing; command.LockedBy = workerId; command.LockedUntil = now.AddSeconds(Math.Clamp(settings.LockSeconds, 5, 300)); command.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await publisher.PublishAsync(command.Topic, command.PayloadJson, cancellationToken);
            await MarkSentAsync(command.Id, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "MQTT publish failed for outbox command {CommandId}.", command.Id);
            await MarkFailureAsync(command.Id, exception.Message, cancellationToken);
        }
        return true;
    }

    private async Task MarkSentAsync(Guid id, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        var command = await db.RobotCommandOutbox.SingleOrDefaultAsync(item => item.Id == id && item.Status == RobotCommandOutboxStatus.Processing && item.LockedBy == workerId, cancellationToken);
        if (command is null) return;
        var now = DateTime.UtcNow; command.Status = RobotCommandOutboxStatus.Sent; command.SentAt = now; command.LockedBy = null; command.LockedUntil = null; command.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailureAsync(Guid id, string error, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        var command = await db.RobotCommandOutbox.SingleOrDefaultAsync(item => item.Id == id && item.Status == RobotCommandOutboxStatus.Processing && item.LockedBy == workerId, cancellationToken);
        if (command is null) return;
        var now = DateTime.UtcNow; command.AttemptCount++; command.LastError = error.Length <= 2000 ? error : error[..2000]; command.LockedBy = null; command.LockedUntil = null; command.UpdatedAt = now;
        if (command.AttemptCount >= Math.Clamp(settings.MaxAttempts, 1, 100))
        {
            command.Status = RobotCommandOutboxStatus.Failed;
            db.Alerts.Add(new Alert { Id = Guid.NewGuid(), AlertType = "MQTT_DISPATCH_FAILED", Category = AlertCategory.Robot, Severity = AlertSeverity.Critical, Status = AlertStatus.Open, RobotId = command.RobotId, TripId = command.TripId, Message = $"MQTT dispatch failed after {command.AttemptCount} attempts.", CreatedAt = now });
            db.ActivityLogs.Add(new ActivityLog { Timestamp = now, Action = "MQTT_COMMAND_FAILED", TargetObject = $"RobotCommandOutbox:{command.Id:D}" });
        }
        else
        {
            command.Status = RobotCommandOutboxStatus.Pending;
            command.NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, command.AttemptCount)));
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
