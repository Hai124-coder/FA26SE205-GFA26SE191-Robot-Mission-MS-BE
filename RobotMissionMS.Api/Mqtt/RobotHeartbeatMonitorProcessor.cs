using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Api.Realtime;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Mqtt;

/// <summary>Marks connected robots offline when no accepted MQTT state arrives before the heartbeat timeout.</summary>
public sealed class RobotHeartbeatMonitorProcessor(
    IServiceScopeFactory scopes,
    IRobotStateRealtimePublisher? realtime = null,
    ILogger<RobotHeartbeatMonitorProcessor>? logger = null,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<int> MarkStaleRobotsOfflineAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - timeout;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var robots = await db.Robots.FromSqlInterpolated($"""
            SELECT r.* FROM public."Robots" AS r
            WHERE r."IsConnected" = true
              AND (r."LastHeartbeatAt" IS NULL OR r."LastHeartbeatAt" <= {cutoff})
            FOR UPDATE SKIP LOCKED
            """)
            .Include(item => item.Zone)
            .ToListAsync(cancellationToken);

        if (robots.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var offlineAt = clock.GetUtcNow().UtcDateTime;
        foreach (var robot in robots)
            robot.IsConnected = false;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (realtime is not null)
        {
            foreach (var robot in robots)
            {
                try
                {
                    await realtime.PublishUpdatedAsync(robot.Zone.Code, new RobotRealtimeUpdate(
                        robot.Id, robot.Zone.Code, robot.CurrentX, robot.CurrentY, robot.Battery,
                        "OFFLINE", false, null, null, null, offlineAt), cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    logger?.LogError(exception, "Robot {RobotCode} was marked offline but robot.updated could not be published.", robot.RobotCode);
                }
            }
        }

        return robots.Count;
    }
}
