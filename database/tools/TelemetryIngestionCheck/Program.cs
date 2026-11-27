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
if (args.Contains("--lifecycle-only"))
{
    await CheckLifecycleAsync(new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options, now, suffix);
    return;
}
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

static async Task CheckLifecycleAsync(DbContextOptions<RobotMissionDbContext> options, DateTimeOffset now, string suffix)
{
    await using var db = new RobotMissionDbContext(options);
    await using var transaction = await db.Database.BeginTransactionAsync();
    using var provider = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
    var processor = new RobotStateTelemetryProcessor(provider.GetRequiredService<IServiceScopeFactory>(),
        timeProvider: new FixedTimeProvider(now));

    var zone = new Zone { Id = Guid.NewGuid(), Code = $"TL{suffix}", Name = "Lifecycle test", IsActive = true };
    var type = new RobotType { Id = Guid.NewGuid(), TypeCode = $"TL{suffix}", Name = "Lifecycle test", ColorHex = "#000000" };
    var robot = new Robot { Id = Guid.NewGuid(), RobotCode = $"TL-{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id,
        State = RobotState.Inactive, AwaitingFirstConnection = true, CreatedAt = now.UtcDateTime };
    db.AddRange(zone, type, robot);
    await db.SaveChangesAsync();

    var idle = new RobotStateTelemetry(robot.RobotCode, now, 0, 0, 0, 80, 0, "IDLE", null, null, null);
    await processor.ProcessAsync(idle with { Status = "MOVING" });
    if (!robot.IsConnected || !robot.AwaitingFirstConnection || robot.State != RobotState.Inactive)
        throw new InvalidOperationException("First non-idle telemetry must not activate a robot.");
    await processor.ProcessAsync(idle);
    if (robot.AwaitingFirstConnection || robot.State != RobotState.Available)
        throw new InvalidOperationException("First idle telemetry must activate a newly registered robot.");

    var map = new MapVersion { Id = Guid.NewGuid(), ZoneId = zone.Id, VersionNo = 1, SourceJson = "{}",
        WidthM = 1, HeightM = 1, Status = MapVersionStatus.Published, ImportedAt = now.UtcDateTime };
    var trip = new Trip { Id = Guid.NewGuid(), TripCode = $"TL{suffix}", ZoneId = zone.Id, RobotId = robot.Id,
        MapVersionId = map.Id, Status = TripStatus.InProgress, CreatedAt = now.UtcDateTime, StartedAt = now.UtcDateTime };
    robot.State = RobotState.OnTrip;
    robot.SuspendRequestedAt = now.UtcDateTime;
    db.AddRange(map, trip);
    await db.SaveChangesAsync();
    await processor.ProcessAsync(idle);
    if (robot.State != RobotState.OnTrip)
        throw new InvalidOperationException("Idle telemetry must not suspend a robot with unfinished work.");
    trip.Status = TripStatus.Aborted;
    trip.CompletedAt = now.UtcDateTime;
    await db.SaveChangesAsync();
    await processor.ProcessAsync(idle);
    if (robot.State != RobotState.Inactive || robot.SuspendRequestedAt is null)
        throw new InvalidOperationException("Idle telemetry must finalize pending suspension after an aborted trip.");

    robot.SuspendRequestedAt = null;
    robot.DecommissionedAt = now.UtcDateTime;
    robot.IsConnected = false;
    await db.SaveChangesAsync();
    await processor.ProcessAsync(idle);
    if (!robot.IsConnected || robot.State != RobotState.Inactive || robot.DecommissionedAt is null)
        throw new InvalidOperationException("Reconnection must not activate a decommissioned robot.");

    await transaction.RollbackAsync();
    Console.WriteLine("telemetry-lifecycle=verified activation=idle aborted-trip=suspended decommissioned=inactive rollback=complete");
}

sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
