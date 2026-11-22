using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Schedules.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");

await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();

var now = DateTime.UtcNow;
var suffix = Guid.NewGuid().ToString("N")[..8];
var robotType = await db.RobotTypes.FirstOrDefaultAsync(x => x.TypeCode == "WASTE_CART")
    ?? new RobotType { Id = Guid.NewGuid(), TypeCode = "WASTE_CART", Name = "Waste", Slots = 0, ColorHex = "#000000" };
var missionType = await db.MissionTypes.FirstOrDefaultAsync(x => x.Code == "WASTE_COLLECTION")
    ?? new MissionType { Id = Guid.NewGuid(), Code = "WASTE_COLLECTION", Name = "Waste", RobotTypeId = robotType.Id, BasePriority = 1, UsesSlotSensor = false, DepartDelaySec = 0, WaitTimeoutSec = 0, IsActive = true };
if (db.Entry(robotType).State == EntityState.Detached) db.RobotTypes.Add(robotType);
if (db.Entry(missionType).State == EntityState.Detached) db.MissionTypes.Add(missionType);

var zone = new Zone { Id = Guid.NewGuid(), Code = $"T{suffix}", Name = "Schedule worker rollback", IsActive = true };
var location = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = $"L{suffix}", Name = "Stop", MapNodeCode = "N", Kind = LocationKind.WastePoint, IsActive = true };
var robot = new Robot { Id = Guid.NewGuid(), RobotCode = $"R{suffix}", RobotTypeId = robotType.Id, ZoneId = zone.Id, IsConnected = true, State = RobotState.Available, Battery = 100, CreatedAt = now };
var map = new MapVersion { Id = Guid.NewGuid(), ZoneId = zone.Id, VersionNo = 1, SourceJson = "{}", WidthM = 1, HeightM = 1, Status = MapVersionStatus.Published, ImportedAt = now, PublishedAt = now };
db.AddRange(zone, location, robot, map);
await db.SaveChangesAsync();

MissionSchedule AddSchedule(Guid zoneId, string name) => new()
{
    Id = Guid.NewGuid(), Name = name, MissionTypeId = missionType.Id, ZoneId = zoneId,
    DaysOfWeek = [1], RunTime = TimeOnly.FromDateTime(now), IsActive = true, CreatedAt = now
};

MissionScheduleRun AddRun(MissionSchedule schedule, DateTime scheduledFor, bool manual = false) => new()
{
    Id = Guid.NewGuid(), ScheduleId = schedule.Id, ScheduledFor = scheduledFor, IsManual = manual,
    Status = ScheduleRunStatus.WaitingForRobot, CreatedAt = now,
    RouteSnapshot = JsonSerializer.Serialize(new { stops = new[] { new { seq = 1, code = location.Code } } })
};

var schedule = AddSchedule(zone.Id, "positive");
var createdRun = AddRun(schedule, now);
db.AddRange(schedule, createdRun);
await db.SaveChangesAsync();

var processor = new ScheduleRunProcessor(db, new ScheduledRouteDispatchService(db));
if (await processor.EvaluateAsync(createdRun.Id, 15, now) != ScheduledRouteDispatchResult.Created)
    throw new InvalidOperationException("Expected a scheduled mission to be created.");

var created = await db.MissionScheduleRuns.Include(x => x.Mission).SingleAsync(x => x.Id == createdRun.Id);
var trip = await db.Trips.Include(x => x.TripStops).SingleAsync(x => x.Id == created.Mission!.TripId);
if (trip.TripStops.Count != 1) throw new InvalidOperationException("Created trip has wrong stops.");
if (await processor.EvaluateAsync(createdRun.Id, 15, now) != ScheduledRouteDispatchResult.AlreadyCreated)
    throw new InvalidOperationException("Repeat evaluation created a duplicate mission.");
if (await db.Trips.CountAsync() != 1) throw new InvalidOperationException("Duplicate trip detected.");

var waitingRun = AddRun(schedule, now.AddSeconds(1));
db.MissionScheduleRuns.Add(waitingRun);
await db.SaveChangesAsync();
if (await processor.EvaluateAsync(waitingRun.Id, 15, now) != ScheduledRouteDispatchResult.NoEligibleRobot)
    throw new InvalidOperationException("Expected no eligible robot after the first robot was reserved.");
var waiting = await db.MissionScheduleRuns.SingleAsync(x => x.Id == waitingRun.Id);
if (waiting.Status != ScheduleRunStatus.WaitingForRobot || waiting.FailureReason != "NO_ELIGIBLE_ROBOT")
    throw new InvalidOperationException("Waiting run did not retain its reason.");

var lateRun = AddRun(schedule, now.AddMinutes(-16));
db.MissionScheduleRuns.Add(lateRun);
await db.SaveChangesAsync();
await processor.EvaluateAsync(lateRun.Id, 15, now);
var late = await db.MissionScheduleRuns.SingleAsync(x => x.Id == lateRun.Id);
if (late.Status != ScheduleRunStatus.Late || late.LateAlertId is null)
    throw new InvalidOperationException("Late run did not create and link E1 alert.");
await processor.EvaluateAsync(lateRun.Id, 15, now.AddMinutes(1));
if (await db.Alerts.CountAsync(x => x.Id == late.LateAlertId) != 1)
    throw new InvalidOperationException("Late run duplicated E1 alert.");

var noMapZone = new Zone { Id = Guid.NewGuid(), Code = $"M{suffix}", Name = "No map", IsActive = true };
var noMapLocation = new Location { Id = Guid.NewGuid(), ZoneId = noMapZone.Id, Code = $"M{suffix}", Name = "Stop", MapNodeCode = "N", Kind = LocationKind.WastePoint, IsActive = true };
var noMapRobot = new Robot { Id = Guid.NewGuid(), RobotCode = $"M{suffix}", RobotTypeId = robotType.Id, ZoneId = noMapZone.Id, IsConnected = true, State = RobotState.Available, Battery = 100, CreatedAt = now };
var noMapSchedule = AddSchedule(noMapZone.Id, "no-map");
var noMapRun = new MissionScheduleRun { Id = Guid.NewGuid(), ScheduleId = noMapSchedule.Id, ScheduledFor = now, Status = ScheduleRunStatus.WaitingForRobot, CreatedAt = now, RouteSnapshot = JsonSerializer.Serialize(new { stops = new[] { new { seq = 1, code = noMapLocation.Code } } }) };
db.AddRange(noMapZone, noMapLocation, noMapRobot, noMapSchedule, noMapRun);
await db.SaveChangesAsync();
if (await processor.EvaluateAsync(noMapRun.Id, 15, now) != ScheduledRouteDispatchResult.MapNotPublished)
    throw new InvalidOperationException("Expected a missing published map to block dispatch.");
var noMap = await db.MissionScheduleRuns.SingleAsync(x => x.Id == noMapRun.Id);
if (noMap.Status != ScheduleRunStatus.WaitingForRobot || noMap.FailureReason != "MAP_NOT_PUBLISHED")
    throw new InvalidOperationException("Missing map reason was not persisted.");

Console.WriteLine("schedule-worker=verified created=1 idempotent=1 waiting=1 late-e1=1 map-blocked=1 rollback=planned");
await transaction.RollbackAsync();
