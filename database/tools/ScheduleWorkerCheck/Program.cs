using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Schedules.DTOs;
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
    ?? new MissionType { Id = Guid.NewGuid(), Code = "WASTE_COLLECTION", Name = "Waste", RobotTypeId = robotType.Id, BasePriority = 1, UsesSlotSensor = false, DepartDelaySec = 0, WaitTimeoutSec = 0, IsActive = true, Mode = MissionMode.Schedule };
var role = await db.Roles.FirstOrDefaultAsync() ?? new Role { Id = Guid.NewGuid(), Name = $"Role{suffix}" };
var user = await db.Users.FirstOrDefaultAsync() ?? new User { Id = Guid.NewGuid(), Username = $"U{suffix}", PasswordHash = "test", FullName = "Schedule worker test", IsActive = true, RoleId = role.Id, CreatedAt = now };
if (db.Entry(robotType).State == EntityState.Detached) db.RobotTypes.Add(robotType);
if (db.Entry(missionType).State == EntityState.Detached) db.MissionTypes.Add(missionType);
if (db.Entry(role).State == EntityState.Detached) db.Roles.Add(role);
if (db.Entry(user).State == EntityState.Detached) db.Users.Add(user);

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
if (await db.ActivityLogs.CountAsync(x => x.Action == "SCHEDULE_RUN_MISSION_CREATED" && x.TargetObject == $"ScheduleRun:{createdRun.Id:D}" && x.UserId == null) != 1)
    throw new InvalidOperationException("System dispatch activity was not logged.");
if (await processor.EvaluateAsync(createdRun.Id, 15, now) != ScheduledRouteDispatchResult.AlreadyCreated)
    throw new InvalidOperationException("Repeat evaluation created a duplicate mission.");
if (await db.Trips.CountAsync() != 1) throw new InvalidOperationException("Duplicate trip detected.");

var scheduleService = new ScheduleService(db);
var manual = await scheduleService.RunAsync(schedule.Id, user.Id, zone.Code, "manual-run-key");
if (!manual.IsSuccess || manual.Response!.Replayed) throw new InvalidOperationException("Manual run was not created.");
if (await db.ActivityLogs.CountAsync(x => x.Action == "SCHEDULE_RUN_REQUESTED" && x.TargetObject == $"ScheduleRun:{manual.Response.RunId:D}" && x.UserId == user.Id) != 1)
    throw new InvalidOperationException("Manual run activity was not logged with its actor.");
var replay = await scheduleService.RunAsync(schedule.Id, user.Id, zone.Code, "manual-run-key");
if (!replay.IsSuccess || !replay.Response!.Replayed || replay.Response.RunId != manual.Response.RunId) throw new InvalidOperationException("Manual run was not replayed.");
if (await db.ActivityLogs.CountAsync(x => x.Action == "SCHEDULE_RUN_REQUESTED" && x.TargetObject == $"ScheduleRun:{manual.Response.RunId:D}") != 1)
    throw new InvalidOperationException("Manual run replay duplicated its activity.");
var blocked = await scheduleService.RunAsync(schedule.Id, user.Id, zone.Code, "different-manual-run-key");
if (blocked.Error != ScheduleRunError.RunInProgress) throw new InvalidOperationException("Concurrent manual run was not blocked.");
var scheduleList = await scheduleService.ListAsync(zone.Code);
var scheduleSummary = scheduleList.Single(x => x.Id == schedule.Id);
if (scheduleSummary.LastRunAt is null || scheduleSummary.LastResult != "WAITING_FOR_ROBOT" || scheduleSummary.NextRunAt is null || scheduleSummary.NextRunAt <= now)
    throw new InvalidOperationException("Schedule list metadata is incorrect.");

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
if (await db.ActivityLogs.CountAsync(x => x.Action == "SCHEDULE_RUN_LATE" && x.TargetObject == $"ScheduleRun:{lateRun.Id:D}" && x.UserId == null) != 1)
    throw new InvalidOperationException("Late schedule activity was not logged.");
await processor.EvaluateAsync(lateRun.Id, 15, now.AddMinutes(1));
if (await db.Alerts.CountAsync(x => x.Id == late.LateAlertId) != 1)
    throw new InvalidOperationException("Late run duplicated E1 alert.");
if (await db.ActivityLogs.CountAsync(x => x.Action == "SCHEDULE_RUN_LATE" && x.TargetObject == $"ScheduleRun:{lateRun.Id:D}") != 1)
    throw new InvalidOperationException("Late schedule activity was duplicated.");

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

var request = new ScheduleRequest { Name = "audit", MissionTypeCode = "WASTE_COLLECTION", Floor = zone.Code, DaysOfWeek = [1], RunTime = now.ToString("HH:mm"), Stops = [location.Code], IsActive = true };
var audited = await scheduleService.CreateAsync(user.Id, zone.Code, request);
if (audited is null) throw new InvalidOperationException("Schedule audit fixture was not created.");
var updatedRequest = new ScheduleRequest { Name = "audit updated", MissionTypeCode = "WASTE_COLLECTION", Floor = zone.Code, DaysOfWeek = [1], RunTime = now.ToString("HH:mm"), Stops = [location.Code], IsActive = true };
var updated = await scheduleService.UpdateAsync(audited.Id, user.Id, zone.Code, updatedRequest);
if (updated is null || await scheduleService.SetStatusAsync(audited.Id, user.Id, zone.Code, false) != true
    || await scheduleService.SetStatusAsync(audited.Id, user.Id, zone.Code, false) != true
    || await scheduleService.DeleteAsync(audited.Id, user.Id, zone.Code) != true)
    throw new InvalidOperationException("Schedule audit fixture could not be modified.");
var scheduleTarget = $"Schedule:{audited.Id:D}";
var expectedActions = new[] { "SCHEDULE_CREATED", "SCHEDULE_UPDATED", "SCHEDULE_STATUS_CHANGED", "SCHEDULE_DELETED" };
if (await db.ActivityLogs.CountAsync(x => x.UserId == user.Id && x.TargetObject == scheduleTarget && expectedActions.Contains(x.Action)) != expectedActions.Length)
    throw new InvalidOperationException("Schedule CRUD activities are incomplete or duplicated.");

Console.WriteLine("schedule-worker=verified created=1 idempotent=1 waiting=1 late-e1=1 map-blocked=1 activity=crud-worker rollback=planned");
await transaction.RollbackAsync();
