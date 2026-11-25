using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.ActivityLogs.Services;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Robots.Services;

/// <summary>
/// Queues immediate safety commands in the same durable MQTT outbox used for trip dispatch.
/// A command is accepted only while the robot has a current MQTT heartbeat, so the API does
/// not claim delivery to an offline robot.
/// </summary>
public sealed class RobotControlCommandService(
    RobotMissionDbContext db,
    TimeProvider? timeProvider = null,
    IActivityRealtimePublisher? activityRealtime = null) : IRobotControlCommandService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<RobotControlCommandResult> EnqueueAsync(
        Guid actorUserId,
        Guid robotId,
        string? operatorFloor,
        RobotControlCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || robotId == Guid.Empty || request is null)
            return RobotControlCommandResult.Failure(RobotControlCommandError.InvalidRequest);

        var command = request.Command?.Trim().ToUpperInvariant();
        if (command is not ("STOP" or "RESUME"))
            return RobotControlCommandResult.Failure(RobotControlCommandError.InvalidRequest);

        var robot = await db.Robots
            .Include(value => value.Zone)
            .SingleOrDefaultAsync(value => value.Id == robotId, cancellationToken);
        if (robot is null)
            return RobotControlCommandResult.Failure(RobotControlCommandError.RobotNotFound);

        if (!string.IsNullOrWhiteSpace(operatorFloor)
            && !string.Equals(robot.Zone.Code, operatorFloor.Trim(), StringComparison.OrdinalIgnoreCase))
            return RobotControlCommandResult.Failure(RobotControlCommandError.FloorForbidden);

        if (!robot.IsConnected)
            return RobotControlCommandResult.Failure(RobotControlCommandError.RobotNotConnected);

        var now = clock.GetUtcNow().UtcDateTime;
        var commandId = Guid.NewGuid();
        var outbox = new RobotCommandOutbox
        {
            Id = commandId,
            CommandType = command,
            RobotId = robot.Id,
            Topic = $"rmms/v1/{robot.RobotCode}/command",
            PayloadJson = JsonSerializer.Serialize(new
            {
                commandId,
                command,
                issuedAt = now,
            }),
            Status = RobotCommandOutboxStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.RobotCommandOutbox.Add(outbox);
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = actorUserId,
            Timestamp = now,
            Action = $"ROBOT_{command}_QUEUED",
            TargetObject = $"RobotCommandOutbox:{commandId:D}",
        });
        await db.SaveChangesAsync(cancellationToken);

        if (activityRealtime is not null)
        {
            await activityRealtime.PublishAsync(
                robot.Zone.Code,
                new ActivityRealtimeEvent(now, $"ROBOT_{command}_QUEUED", $"RobotCommandOutbox:{commandId:D}", actorUserId),
                cancellationToken);
        }

        return RobotControlCommandResult.Success(new RobotControlCommandResponse
        {
            CommandId = commandId,
            RobotId = robot.Id,
            Command = command,
            Status = "PENDING",
            IssuedAt = now,
        });
    }
}
