using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.ChargingSessions.Services;
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
var zone = new Zone { Id = Guid.NewGuid(), Code = $"C{suffix}", Name = "Charging rollback", IsActive = true };
var type = new RobotType { Id = Guid.NewGuid(), TypeCode = $"T{suffix}", Name = "Test", Slots = 0, ColorHex = "#000000" };
var location = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = $"L{suffix}", Name = "Charger", MapNodeCode = "N", Kind = LocationKind.Charger, IsActive = true };
var station = new ChargingStation { Id = Guid.NewGuid(), LocationId = location.Id, Name = $"S{suffix}", StationType = "CHARGER", Capacity = 1, Status = StationStatus.Operational };
var firstRobot = new Robot { Id = Guid.NewGuid(), RobotCode = $"R1{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id, HomeChargingStationId = station.Id, IsConnected = true, State = RobotState.Available, Battery = 40, CreatedAt = now };
var secondRobot = new Robot { Id = Guid.NewGuid(), RobotCode = $"R2{suffix}", RobotTypeId = type.Id, ZoneId = zone.Id, IsConnected = true, State = RobotState.Available, Battery = 30, CreatedAt = now };
db.AddRange(zone, type, location, station, firstRobot, secondRobot);
await db.SaveChangesAsync();

var service = new ChargingSessionService(db);
var queuedFirst = await service.QueueToHomeStationAsync(firstRobot.Id, now);
if (!queuedFirst.IsSuccess || queuedFirst.Response!.Status != "QUEUED") throw new InvalidOperationException("First robot was not queued.");
if ((await db.Robots.SingleAsync(x => x.Id == firstRobot.Id)).State != RobotState.OnTrip) throw new InvalidOperationException("Queued robot state is wrong.");

var startedFirst = await service.StartAsync(queuedFirst.Response.Id, now.AddMinutes(1));
if (!startedFirst.IsSuccess || startedFirst.Response!.BatteryAtStart != 40 || startedFirst.Response.Status != "CHARGING") throw new InvalidOperationException("First session did not start.");

var queuedSecond = await service.QueueAsync(secondRobot.Id, station.Id, now.AddMinutes(2));
if (!queuedSecond.IsSuccess) throw new InvalidOperationException("Second robot was not queued.");
var blockedSecond = await service.StartAsync(queuedSecond.Response!.Id, now.AddMinutes(3));
if (blockedSecond.Error != ChargingSessionCommandError.StationAtCapacity) throw new InvalidOperationException("Station capacity was not enforced.");

var completedFirst = await service.CompleteAsync(queuedFirst.Response.Id, 85, now.AddMinutes(4));
if (!completedFirst.IsSuccess || completedFirst.Response!.Status != "COMPLETED" || completedFirst.Response.BatteryAtEnd != 85) throw new InvalidOperationException("First session did not complete.");
if ((await db.Robots.SingleAsync(x => x.Id == firstRobot.Id)).State != RobotState.Available) throw new InvalidOperationException("Completed robot was not released.");

var startedSecond = await service.StartAsync(queuedSecond.Response.Id, now.AddMinutes(5));
if (!startedSecond.IsSuccess) throw new InvalidOperationException("Freed station did not accept queued robot.");
var interruptedSecond = await service.InterruptAsync(queuedSecond.Response!.Id, 35, now.AddMinutes(6));
if (!interruptedSecond.IsSuccess || interruptedSecond.Response!.Status != "INTERRUPTED") throw new InvalidOperationException("Second session did not interrupt.");

var secondFirstSession = await service.QueueAsync(firstRobot.Id, station.Id, now.AddMinutes(7));
if (!secondFirstSession.IsSuccess) throw new InvalidOperationException("Charging history prevented a new session.");
var list = await service.ListAsync(new(), zone.Code);
if (!list.IsSuccess || list.Response!.Total != 3 || list.Response.Items.Any(x => x.Floor != zone.Code)) throw new InvalidOperationException("History query is incorrect.");

Console.WriteLine("charging-session=verified queued=2 capacity=1 completed=1 interrupted=1 history=1 rollback=planned");
await transaction.RollbackAsync();
