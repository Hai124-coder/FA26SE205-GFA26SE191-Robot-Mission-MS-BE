using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Api.Realtime;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Mqtt;

/// <summary>Persists a valid inbound robot-state message as the current robot snapshot and a telemetry history row.</summary>
public sealed class RobotStateTelemetryProcessor(
    IServiceScopeFactory scopes,
    IRobotStateRealtimePublisher? realtime = null,
    ILogger<RobotStateTelemetryProcessor>? logger = null,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<RobotStateTelemetryProcessResult> ProcessAsync(
        RobotStateTelemetry state,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        var robot = await db.Robots.Include(item => item.Zone).SingleOrDefaultAsync(
            item => item.RobotCode == state.RobotId,
            cancellationToken);

        if (robot is null)
            return RobotStateTelemetryProcessResult.RobotNotFound;

        var receivedAt = clock.GetUtcNow().UtcDateTime;
        robot.CurrentX = state.X;
        robot.CurrentY = state.Y;
        robot.Battery = state.Battery;
        robot.LastHeartbeatAt = receivedAt;
        robot.IsConnected = true;

        db.TelemetrySnapshots.Add(new TelemetrySnapshot
        {
            RobotId = robot.Id,
            State = robot.State,
            Timestamp = state.Timestamp.UtcDateTime,
            ReceivedAt = receivedAt,
            BatteryLevel = state.Battery,
            CoordX = state.X,
            CoordY = state.Y,
            HeadingRad = state.Theta,
            Speed = state.Speed,
            ReportedStatus = state.Status,
            OrderId = state.OrderId,
            NextNodeCode = state.NextNodeId,
            AtStopSeq = state.AtStopSeq
        });

        var activeTripId = await db.Trips.AsNoTracking()
            .Where(item => item.RobotId == robot.Id
                && (item.Status == TripStatus.Dispatched || item.Status == TripStatus.InProgress))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (realtime is not null)
        {
            try
            {
                await realtime.PublishUpdatedAsync(robot.Zone.Code, new RobotRealtimeUpdate(
                    robot.Id, robot.Zone.Code, state.X, state.Y, state.Battery, state.Status,
                    true, activeTripId, state.NextNodeId, state.AtStopSeq, receivedAt), cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger?.LogError(exception, "Telemetry was persisted but robot.updated could not be published for {RobotCode}.", robot.RobotCode);
            }
        }
        return RobotStateTelemetryProcessResult.Persisted;
    }
}

public sealed record RobotStateTelemetry(
    string RobotId,
    DateTimeOffset Timestamp,
    double X,
    double Y,
    double Theta,
    int Battery,
    double Speed,
    string Status,
    string? OrderId,
    string? NextNodeId,
    int? AtStopSeq);

public enum RobotStateTelemetryProcessResult
{
    Persisted,
    RobotNotFound
}
