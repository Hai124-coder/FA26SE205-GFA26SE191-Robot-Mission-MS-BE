using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Dashboard.Services;
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
var service = new DashboardService(db, TimeProvider.System);
var baseline = await service.GetOverviewAsync();
var suffix = Guid.NewGuid().ToString("N")[..8];
var zone = new Zone { Id = Guid.NewGuid(), Code = $"D{suffix}", Name = "Dashboard rollback", IsActive = true };
var robotType = new RobotType { Id = Guid.NewGuid(), TypeCode = $"D{suffix}", Name = "Dashboard", Slots = 0, ColorHex = "#000000" };
var missionType = new MissionType { Id = Guid.NewGuid(), Code = $"D{suffix}", Name = "Dashboard", RobotTypeId = robotType.Id, BasePriority = 1, UsesSlotSensor = false, DepartDelaySec = 0, WaitTimeoutSec = 0, IsActive = true, Mode = MissionMode.Batch };
var schedule = new MissionSchedule { Id = Guid.NewGuid(), Name = $"D{suffix}", MissionTypeId = missionType.Id, ZoneId = zone.Id, DaysOfWeek = [1], RunTime = TimeOnly.FromDateTime(now), IsActive = true, CreatedAt = now };
var fromLocation = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = $"F{suffix}", Name = "From", MapNodeCode = "N1", Kind = LocationKind.Storage, IsActive = true };
var toLocation = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = $"T{suffix}", Name = "To", MapNodeCode = "N2", Kind = LocationKind.Dept, IsActive = true };
var states = new[] { RobotState.OnTrip, RobotState.Charging, RobotState.Available, RobotState.Maintenance, RobotState.Error, RobotState.Inactive };
var robots = states.Select((state, index) => new Robot { Id = Guid.NewGuid(), RobotCode = $"R{index}{suffix}", RobotTypeId = robotType.Id, ZoneId = zone.Id, State = state, IsConnected = true, Battery = 50, CreatedAt = now }).ToList();
Mission Mission(string code, MissionStatus status, DateTime createdAt) => new() { Id = Guid.NewGuid(), MissionCode = code, MissionTypeId = missionType.Id, ZoneId = zone.Id, ScheduleId = schedule.Id, ScheduledFor = createdAt.AddTicks(Math.Abs((long)Guid.NewGuid().GetHashCode())), Status = status, Priority = MissionPriority.Normal, CreatedAt = createdAt };
var onTime = Mission($"M1{suffix}", MissionStatus.Completed, now); onTime.StartedAt = now.AddMinutes(-20); onTime.CompletedAt = now.AddMinutes(-5); onTime.DeadlineAt = now.AddMinutes(-1);
var lateHuman = Mission($"M2{suffix}", MissionStatus.CompletedByHuman, now); lateHuman.StartedAt = now.AddMinutes(-30); lateHuman.CompletedAt = now.AddMinutes(-5); lateHuman.DeadlineAt = now.AddMinutes(-10);
var pendingToday = Mission($"M3{suffix}", MissionStatus.Pending, now);
var assignedToday = Mission($"M4{suffix}", MissionStatus.Assigned, now);
var rescheduledOld = Mission($"M5{suffix}", MissionStatus.Rescheduled, now.AddDays(-2));
var withoutDeadline = Mission($"M6{suffix}", MissionStatus.Completed, now); withoutDeadline.CompletedAt = now.AddMinutes(-2);
var openAlert = new Alert { Id = Guid.NewGuid(), AlertType = "TEST", ZoneId = zone.Id, Message = "Open", Category = AlertCategory.System, Severity = AlertSeverity.Warning, Status = AlertStatus.Open, CreatedAt = now };
var resolvedAlert = new Alert { Id = Guid.NewGuid(), AlertType = "TEST", ZoneId = zone.Id, Message = "Resolved", Category = AlertCategory.System, Severity = AlertSeverity.Warning, Status = AlertStatus.Resolved, CreatedAt = now, ResolvedAt = now };
db.AddRange(zone, robotType, missionType, schedule, fromLocation, toLocation, onTime, lateHuman, pendingToday, assignedToday, rescheduledOld, withoutDeadline, openAlert, resolvedAlert);
db.Robots.AddRange(robots);
await db.SaveChangesAsync();

var overview = await service.GetOverviewAsync();
if (overview.RobotCounts.OnTrip != baseline.RobotCounts.OnTrip + 1 || overview.RobotCounts.Charging != baseline.RobotCounts.Charging + 1 || overview.RobotCounts.Available != baseline.RobotCounts.Available + 1 || overview.RobotCounts.Maintenance != baseline.RobotCounts.Maintenance + 1 || overview.RobotCounts.Error != baseline.RobotCounts.Error + 1 || overview.RobotCounts.Inactive != baseline.RobotCounts.Inactive + 1)
    throw new InvalidOperationException("Robot counts are incorrect.");
if (overview.MissionCounts.Pending != baseline.MissionCounts.Pending + 1 || overview.MissionCounts.Assigned != baseline.MissionCounts.Assigned + 1 || overview.MissionCounts.Completed != baseline.MissionCounts.Completed + 2 || overview.MissionCounts.CompletedByHuman != baseline.MissionCounts.CompletedByHuman + 1)
    throw new InvalidOperationException("Today's mission counts are incorrect.");
if (overview.QueueCount != baseline.QueueCount + 2 || overview.OpenIncidentCount != baseline.OpenIncidentCount + 1)
    throw new InvalidOperationException("Queue or open incident count is incorrect.");
if (overview.OnTime.Eligible != baseline.OnTime.Eligible + 2 || overview.OnTime.OnTime != baseline.OnTime.OnTime + 1)
    throw new InvalidOperationException("On-time calculation is incorrect.");
Console.WriteLine("dashboard=verified robots=1x6 queue>=2 incidents>=1 on-time=1/2 rollback=planned");
await transaction.RollbackAsync();
