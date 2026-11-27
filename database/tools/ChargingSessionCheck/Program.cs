using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.ChargingStations.Services;
using RobotMissionMS.Business.Features.Locations.DTOs;
using RobotMissionMS.Business.Features.Locations.Services;
using RobotMissionMS.Business.Features.ChargingSessions.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Implementations;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");

await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();

var now = DateTime.UtcNow;
var suffix = Guid.NewGuid().ToString("N")[..8];
var role = new Role { Id = Guid.NewGuid(), Name = $"C{suffix}" };
var actor = new User { Id = Guid.NewGuid(), Username = $"c{suffix}", PasswordHash = "test", FullName = "Charging actor", IsActive = true, RoleId = role.Id, CreatedAt = now };
var zone = new Zone { Id = Guid.NewGuid(), Code = $"C{suffix}", Name = "Charging rollback", IsActive = true };
var map = new MapVersion { Id = Guid.NewGuid(), ZoneId = zone.Id, VersionNo = 1, SourceJson = "{}", WidthM = 1, HeightM = 1,
    Status = MapVersionStatus.Published, ImportedByUserId = actor.Id, ImportedAt = now, PublishedByUserId = actor.Id, PublishedAt = now };
var mapNode = new MapNode { Id = Guid.NewGuid(), MapVersionId = map.Id, Code = $"N{suffix}", NodeType = NodeType.Waypoint, CoordX = 0, CoordY = 0 };
var type = new RobotType { Id = Guid.NewGuid(), TypeCode = $"T{suffix}", Name = "Test", Slots = 0, ColorHex = "#000000" };
var location = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = $"L{suffix}", Name = "Charger", MapNodeCode = "N", Kind = LocationKind.Charger, IsActive = true };
var station = new ChargingStation { Id = Guid.NewGuid(), LocationId = location.Id, Name = $"S{suffix}", StationType = "CHARGER", Capacity = 1, Status = StationStatus.Operational };
var firstRobot = new Robot { Id = Guid.NewGuid(), RobotCode = $"R1{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id, HomeChargingStationId = station.Id, IsConnected = true, State = RobotState.Available, Battery = 40, CreatedAt = now };
var secondRobot = new Robot { Id = Guid.NewGuid(), RobotCode = $"R2{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id, IsConnected = true, State = RobotState.Available, Battery = 30, CreatedAt = now };
db.AddRange(role, actor, zone, map, mapNode, type, location, station, firstRobot, secondRobot);
await db.SaveChangesAsync();

var locationService = new LocationService(new LocationRepository(db));
var createdChargingLocation = await locationService.CreateLocationAsync(actor.Id, new CreateLocationRequest
{
    Code = $"LNEW{suffix}", Name = "New charger", Floor = zone.Code, Kind = "CHARGER", MapNodeCode = mapNode.Code,
    ChargingStation = new CreateChargingStationRequest { Name = "New charger station", StationType = "STANDARD", Capacity = 2 }
});
if (!createdChargingLocation.IsSuccess
    || !await db.ChargingStations.AnyAsync(value => value.LocationId == createdChargingLocation.Response!.Id
        && value.StationType == "STANDARD" && value.Capacity == 2))
    throw new InvalidOperationException("Creating a charging location did not create its station atomically.");
var chargingKindChange = await locationService.UpdateLocationAsync(actor.Id, createdChargingLocation.Response!.Code,
    new UpdateLocationRequest { Name = "New charger", Floor = zone.Code, Kind = "DEPT", MapNodeCode = mapNode.Code });
if (chargingKindChange.Error != UpdateLocationError.ChargingStationKindChangeNotAllowed)
    throw new InvalidOperationException("A charging location was incorrectly allowed to change kind.");
var missingStation = await locationService.CreateLocationAsync(actor.Id, new CreateLocationRequest
{ Code = $"LMISS{suffix}", Name = "Missing station", Floor = zone.Code, Kind = "CHARGER", MapNodeCode = mapNode.Code });
if (missingStation.Error != CreateLocationError.ChargingStationRequired)
    throw new InvalidOperationException("A charging location without station configuration was accepted.");

var service = new ChargingSessionService(db);
var queuedFirst = await service.QueueToHomeStationAsync(firstRobot.Id, actor.Id, now);
if (!queuedFirst.IsSuccess || queuedFirst.Response!.Status != "QUEUED") throw new InvalidOperationException("First robot was not queued.");
if ((await db.Robots.SingleAsync(x => x.Id == firstRobot.Id)).State != RobotState.OnTrip) throw new InvalidOperationException("Queued robot state is wrong.");

var startedFirst = await service.StartAsync(queuedFirst.Response.Id, now.AddMinutes(1));
if (!startedFirst.IsSuccess || startedFirst.Response!.BatteryAtStart != 40 || startedFirst.Response.Status != "CHARGING") throw new InvalidOperationException("First session did not start.");

var queuedSecond = await service.QueueAsync(secondRobot.Id, station.Id, actor.Id, now.AddMinutes(2));
if (!queuedSecond.IsSuccess) throw new InvalidOperationException("Second robot was not queued.");
var stationService = new ChargingStationService(new ChargingStationRepository(db));
var stationSnapshot = (await stationService.GetChargingStationsAsync(new() { Floor = zone.Code }, null)).Response!.Items.Single();
if (stationSnapshot.UsedPorts != 1 || stationSnapshot.FreePorts != 0
    || stationSnapshot.ChargingRobots.Select(robot => robot.Id).Single() != firstRobot.Id
    || stationSnapshot.QueuedRobots.Select(robot => robot.Id).Single() != secondRobot.Id
    || stationSnapshot.QueuedRobots.Single().QueuedAt != now.AddMinutes(2))
    throw new InvalidOperationException("Charging station snapshot did not separate occupied ports from the queue.");
var deactivateCharger = await locationService.UpdateLocationStatusAsync(actor.Id, location.Code,
    new UpdateLocationStatusRequest { Active = false });
if (deactivateCharger.Error != UpdateLocationStatusError.InUse
    || deactivateCharger.Usage?.ActiveChargingSessions != 2)
    throw new InvalidOperationException("A charger with queued or charging robots was incorrectly allowed to deactivate.");
var blockedSecond = await service.StartAsync(queuedSecond.Response!.Id, now.AddMinutes(3));
if (blockedSecond.Error != ChargingSessionCommandError.StationAtCapacity) throw new InvalidOperationException("Station capacity was not enforced.");

var completedFirst = await service.CompleteAsync(queuedFirst.Response.Id, 85, now.AddMinutes(4));
if (!completedFirst.IsSuccess || completedFirst.Response!.Status != "COMPLETED" || completedFirst.Response.BatteryAtEnd != 85) throw new InvalidOperationException("First session did not complete.");
if ((await db.Robots.SingleAsync(x => x.Id == firstRobot.Id)).State != RobotState.Available) throw new InvalidOperationException("Completed robot was not released.");

var startedSecond = await service.StartAsync(queuedSecond.Response.Id, now.AddMinutes(5));
if (!startedSecond.IsSuccess) throw new InvalidOperationException("Freed station did not accept queued robot.");
var interruptedSecond = await service.InterruptAsync(queuedSecond.Response!.Id, 35, now.AddMinutes(6));
if (!interruptedSecond.IsSuccess || interruptedSecond.Response!.Status != "INTERRUPTED") throw new InvalidOperationException("Second session did not interrupt.");

var secondFirstSession = await service.QueueAsync(firstRobot.Id, station.Id, actor.Id, now.AddMinutes(7));
if (!secondFirstSession.IsSuccess) throw new InvalidOperationException("Charging history prevented a new session.");
var list = await service.ListAsync(new(), zone.Code);
if (!list.IsSuccess || list.Response!.Total != 3 || list.Response.Items.Any(x => x.Floor != zone.Code)) throw new InvalidOperationException("History query is incorrect.");
var robots = new RobotRepository(db);
if (await robots.UpdateRobotAsync(secondRobot.Id, station.Id, actor.Id) is null
    || !(await robots.MaintenanceRobotAsync(secondRobot.Id, actor.Id)).IsSuccess
    || !(await robots.BackToServiceAsync(secondRobot.Id, actor.Id)).IsSuccess
    || !(await robots.SuspendRobotAsync(secondRobot.Id, actor.Id)).IsSuccess
    || !(await robots.ResumeRobotAsync(secondRobot.Id, actor.Id)).IsSuccess
    || !(await robots.DeleteRobotAsync(secondRobot.Id, actor.Id)).IsSuccess)
    throw new InvalidOperationException("Robot state command fixture failed.");
var createdRobot = await robots.CreateRobotAsync($"R3{suffix}", type.Id, zone.Id, station.Id, actor.Id);
if (!createdRobot.IsSuccess)
    throw new InvalidOperationException("Robot creation activity fixture failed.");
var robotActions = new[] { "ROBOT_UPDATED", "ROBOT_MAINTENANCE_ENTERED", "ROBOT_BACK_IN_SERVICE", "ROBOT_SUSPENDED", "ROBOT_RESUMED", "ROBOT_DECOMMISSIONED", "ROBOT_CREATED" };
if (await db.ActivityLogs.CountAsync(x => x.UserId == actor.Id && robotActions.Contains(x.Action)) != robotActions.Length)
    throw new InvalidOperationException("Robot activity logs are incomplete.");
if (await db.ActivityLogs.CountAsync(x => x.Action == "CHARGING_SESSION_QUEUED" && x.UserId == actor.Id) != 3
    || await db.ActivityLogs.CountAsync(x => x.Action == "CHARGING_SESSION_STARTED" && x.UserId == null) != 2
    || await db.ActivityLogs.CountAsync(x => x.Action == "CHARGING_SESSION_COMPLETED" && x.UserId == null) != 1
    || await db.ActivityLogs.CountAsync(x => x.Action == "CHARGING_SESSION_INTERRUPTED" && x.UserId == null) != 1)
    throw new InvalidOperationException("Charging activity logs are incorrect.");

Console.WriteLine("charging-session=verified station-created=atomic kind-change=blocked station-snapshot=charging,queued charger-deactivate=blocked queued=3 capacity=1 completed=1 interrupted=1 activity=actor-system rollback=planned");
await transaction.RollbackAsync();
