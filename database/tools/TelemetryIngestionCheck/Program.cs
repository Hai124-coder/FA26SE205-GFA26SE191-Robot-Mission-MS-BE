using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RobotMissionMS.Api.Mqtt;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
var now = new DateTimeOffset(2026, 11, 23, 10, 15, 0, TimeSpan.Zero);
var suffix = Guid.NewGuid().ToString("N")[..8];
var zoneId = Guid.NewGuid();
var typeId = Guid.NewGuid();
var robotId = Guid.NewGuid();

await using var dataSource = DatabaseDataSource.Create(connectionString);
var services = new ServiceCollection();
services.AddDbContext<RobotMissionDbContext>(options => options.UseNpgsql(dataSource));
using var provider = services.BuildServiceProvider();
var processor = new RobotStateTelemetryProcessor(
    provider.GetRequiredService<IServiceScopeFactory>(),
    timeProvider: new FixedTimeProvider(now));

try
{
    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        db.AddRange(
            new Zone { Id = zoneId, Code = $"TI{suffix}", Name = "Telemetry check", IsActive = true },
            new RobotType { Id = typeId, TypeCode = $"TI{suffix}", Name = "Telemetry check", Slots = 1, ColorHex = "#000000" },
            new Robot { Id = robotId, RobotCode = $"MED-{suffix}", RobotTypeId = typeId, ZoneId = zoneId, State = RobotState.Available, IsConnected = false, Battery = 10, CreatedAt = now.UtcDateTime });
        await db.SaveChangesAsync();
    }

    var state = new RobotStateTelemetry($"MED-{suffix}", now.AddSeconds(-1), 2.5, 1.2, 0.25, 92, 0.4, "MOVING", null, "F2-N02", 1);
    var persisted = await processor.ProcessAsync(state);
    var unknown = await processor.ProcessAsync(state with { RobotId = $"UNKNOWN-{suffix}" });
    if (persisted != RobotStateTelemetryProcessResult.Persisted || unknown != RobotStateTelemetryProcessResult.RobotNotFound)
        throw new InvalidOperationException("Processor result was incorrect.");

    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        var robot = await db.Robots.SingleAsync(item => item.Id == robotId);
        var snapshot = await db.TelemetrySnapshots.SingleAsync(item => item.RobotId == robotId);
        if (robot.CurrentX != 2.5 || robot.CurrentY != 1.2 || robot.Battery != 92 || !robot.IsConnected || robot.LastHeartbeatAt != now.UtcDateTime)
            throw new InvalidOperationException("Robot snapshot was not updated.");
        if (snapshot.Timestamp != state.Timestamp.UtcDateTime || snapshot.ReceivedAt != now.UtcDateTime || snapshot.BatteryLevel != 92 || snapshot.CoordX != 2.5 || snapshot.CoordY != 1.2 || snapshot.HeadingRad != 0.25 || snapshot.Speed != 0.4 || snapshot.ReportedStatus != "MOVING" || snapshot.NextNodeCode != "F2-N02" || snapshot.AtStopSeq != 1 || snapshot.State != RobotState.Available)
            throw new InvalidOperationException("Telemetry snapshot was not persisted correctly.");
    }

    var heartbeat = new RobotHeartbeatMonitorProcessor(
        provider.GetRequiredService<IServiceScopeFactory>(),
        timeProvider: new FixedTimeProvider(now.AddSeconds(6)));
    if (await heartbeat.MarkStaleRobotsOfflineAsync(TimeSpan.FromSeconds(5)) != 1)
        throw new InvalidOperationException("Heartbeat monitor did not mark the stale robot offline.");
    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        if (await db.Robots.Where(item => item.Id == robotId).Select(item => item.IsConnected).SingleAsync())
            throw new InvalidOperationException("Stale robot is still connected.");
    }

    Console.WriteLine("telemetry-ingestion=verified persisted=1 unknown=ignored snapshots=1 heartbeat=offline");
}
finally
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
    await db.TelemetrySnapshots.Where(item => item.RobotId == robotId).ExecuteDeleteAsync();
    await db.Robots.Where(item => item.Id == robotId).ExecuteDeleteAsync();
    await db.RobotTypes.Where(item => item.Id == typeId).ExecuteDeleteAsync();
    await db.Zones.Where(item => item.Id == zoneId).ExecuteDeleteAsync();
}

sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
