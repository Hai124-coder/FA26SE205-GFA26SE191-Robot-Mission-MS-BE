using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.Business.Features.Robots.Services;
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
var role = new Role { Id = Guid.NewGuid(), Name = $"RC{suffix}" };
var actor = new User
{
    Id = Guid.NewGuid(), Username = $"rc{suffix}", PasswordHash = "test", FullName = "Robot command actor",
    IsActive = true, RoleId = role.Id, CreatedAt = now,
};
var zone = new Zone { Id = Guid.NewGuid(), Code = $"RC{suffix}", Name = "Robot command rollback", IsActive = true };
var otherZone = new Zone { Id = Guid.NewGuid(), Code = $"RO{suffix}", Name = "Other floor", IsActive = true };
var type = new RobotType { Id = Guid.NewGuid(), TypeCode = $"RC{suffix}", Name = "Test", Slots = 0, ColorHex = "#000000" };
var onlineRobot = new Robot
{
    Id = Guid.NewGuid(), RobotCode = $"MED-{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id,
    IsConnected = true, Battery = 100, State = RobotState.Available, CreatedAt = now,
};
var offlineRobot = new Robot
{
    Id = Guid.NewGuid(), RobotCode = $"OFF-{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id,
    IsConnected = false, Battery = 100, State = RobotState.Available, CreatedAt = now,
};

db.AddRange(role, actor, zone, otherZone, type, onlineRobot, offlineRobot);
await db.SaveChangesAsync();

var service = new RobotControlCommandService(db);
var stop = await service.EnqueueAsync(actor.Id, onlineRobot.Id, zone.Code, new RobotControlCommandRequest { Command = " stop " });
if (!stop.IsSuccess || stop.Response!.Command != "STOP" || stop.Response.Status != "PENDING")
    throw new InvalidOperationException("STOP command was not accepted.");

var outbox = await db.RobotCommandOutbox.SingleAsync(value => value.Id == stop.Response.CommandId);
if (outbox.CommandType != "STOP" || outbox.TripId is not null || outbox.PlanId is not null
    || outbox.Topic != $"rmms/v1/{onlineRobot.RobotCode}/command" || outbox.Status != RobotCommandOutboxStatus.Pending)
    throw new InvalidOperationException("STOP outbox row has the wrong shape.");

using var payload = JsonDocument.Parse(outbox.PayloadJson);
if (payload.RootElement.GetProperty("commandId").GetGuid() != outbox.Id
    || payload.RootElement.GetProperty("command").GetString() != "STOP"
    || payload.RootElement.GetProperty("issuedAt").ValueKind != JsonValueKind.String)
    throw new InvalidOperationException("STOP payload does not meet the MQTT command contract.");

var resume = await service.EnqueueAsync(actor.Id, onlineRobot.Id, zone.Code, new RobotControlCommandRequest { Command = "RESUME" });
if (!resume.IsSuccess || resume.Response!.Command != "RESUME")
    throw new InvalidOperationException("RESUME command was not accepted.");

var invalid = await service.EnqueueAsync(actor.Id, onlineRobot.Id, zone.Code, new RobotControlCommandRequest { Command = "MOVE" });
if (invalid.Error != RobotControlCommandError.InvalidRequest)
    throw new InvalidOperationException("Invalid command was accepted.");

var forbidden = await service.EnqueueAsync(actor.Id, onlineRobot.Id, otherZone.Code, new RobotControlCommandRequest { Command = "STOP" });
if (forbidden.Error != RobotControlCommandError.FloorForbidden)
    throw new InvalidOperationException("Cross-floor command was accepted.");

var offline = await service.EnqueueAsync(actor.Id, offlineRobot.Id, zone.Code, new RobotControlCommandRequest { Command = "STOP" });
if (offline.Error != RobotControlCommandError.RobotNotConnected)
    throw new InvalidOperationException("Offline command was accepted.");

var activities = await db.ActivityLogs
    .Where(value => value.UserId == actor.Id && value.TargetObject.StartsWith("RobotCommandOutbox:"))
    .Select(value => value.Action)
    .ToListAsync();
if (!activities.OrderBy(value => value).SequenceEqual(new[] { "ROBOT_RESUME_QUEUED", "ROBOT_STOP_QUEUED" }.OrderBy(value => value)))
    throw new InvalidOperationException("Robot control activity audit is incomplete.");

var robots = new RobotService(new RobotRepository(db));
var scoped = await robots.GetRobotsAsync(new RobotListQuery { Floor = otherZone.Code }, zone.Code);
if (!scoped.IsSuccess || scoped.Response is not { Count: 2 }
    || scoped.Response.Any(robot => robot.Floor != zone.Code)
    || await robots.FindRobotFloorAsync(onlineRobot.Id) != zone.Code)
    throw new InvalidOperationException("Operator floor must take precedence over the list filter.");

var at = now.Date.AddHours(12);
var map = new MapVersion { Id = Guid.NewGuid(), ZoneId = zone.Id, VersionNo = 1, SourceJson = "{}",
    WidthM = 1, HeightM = 1, Status = MapVersionStatus.Published, ImportedAt = at };
var trip = new Trip { Id = Guid.NewGuid(), TripCode = $"TR{suffix}", ZoneId = zone.Id, RobotId = onlineRobot.Id,
    MapVersionId = map.Id, Status = TripStatus.Completed, CreatedAt = at };
var alert = new Alert { Id = Guid.NewGuid(), AlertType = "LOW_BATTERY", RobotId = onlineRobot.Id,
    ZoneId = zone.Id, Message = "Test alert", Category = AlertCategory.Robot, Severity = AlertSeverity.Warning,
    Status = AlertStatus.Open, CreatedAt = at };
var location = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = $"LOC{suffix}",
    Name = "Charger", MapNodeCode = "N", Kind = LocationKind.Charger, IsActive = true };
var station = new ChargingStation { Id = Guid.NewGuid(), LocationId = location.Id, Name = "Test station",
    StationType = "STANDARD", Capacity = 1, Status = StationStatus.Operational };
var queued = new ChargingSession { Id = Guid.NewGuid(), RobotId = onlineRobot.Id,
    ChargingStationId = station.Id, QueuedAt = at, Status = ChargingSessionStatus.Queued };
db.AddRange(map, trip, alert, location, station, queued);
await db.SaveChangesAsync();

var history = (await robots.GetRobotHistoryAsync(onlineRobot.Id, new RobotHistoryQuery { From = at, To = at })).Response;
if (history is null || history.Trips.Single().TripId != trip.Id || history.Alerts.Single().AlertId != alert.Id
    || history.ChargingSessions.Single() is not { StartedAt: null, DurationMinutes: null } session
    || session.SessionId != queued.Id || session.QueuedAt != at)
    throw new InvalidOperationException("History must include the three record types at the inclusive boundary.");

var empty = (await robots.GetRobotHistoryAsync(onlineRobot.Id, new RobotHistoryQuery { From = at.AddSeconds(1), To = at.AddHours(1) })).Response;
if (empty is null || empty.Trips.Count != 0 || empty.Alerts.Count != 0 || empty.ChargingSessions.Count != 0)
    throw new InvalidOperationException("History included records outside the requested window.");

var inverted = await robots.GetRobotHistoryAsync(onlineRobot.Id, new RobotHistoryQuery { From = at.AddDays(1), To = at });
var missing = await robots.GetRobotHistoryAsync(Guid.NewGuid(), new RobotHistoryQuery());
if (inverted.Error != RobotHistoryError.InvalidDateRange || missing.Error != RobotHistoryError.NotFound)
    throw new InvalidOperationException("History did not distinguish an inverted window from a missing robot.");

await transaction.RollbackAsync();
Console.WriteLine("robot-control-command=verified stop=1 resume=1 rejected=invalid,floor,offline rollback=planned");
Console.WriteLine("robot-list-history=verified scope=override history=trip,alert,queued bounds=inclusive rollback=complete");
