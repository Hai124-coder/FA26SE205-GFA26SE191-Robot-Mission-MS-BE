using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Controllers;
using RobotMissionMS.Business.Features.Reports.DTOs;
using RobotMissionMS.Business.Features.Reports.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION") ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();

var suffix = Guid.NewGuid().ToString("N")[..8];
var day = DateTime.UtcNow.Date.AddHours(10);
var zone = new Zone { Id = Guid.NewGuid(), Code = $"O{suffix}", Name = "Operations rollback", IsActive = true };
var robotType = new RobotType { Id = Guid.NewGuid(), TypeCode = $"O{suffix}", Name = "Operations", Slots = 0, ColorHex = "#000000" };
var typeA = new MissionType { Id = Guid.NewGuid(), Code = $"A{suffix}", Name = "A", RobotTypeId = robotType.Id, BasePriority = 1, UsesSlotSensor = false, DepartDelaySec = 0, WaitTimeoutSec = 0, IsActive = true, Mode = MissionMode.Schedule };
var typeB = new MissionType { Id = Guid.NewGuid(), Code = $"B{suffix}", Name = "B", RobotTypeId = robotType.Id, BasePriority = 1, UsesSlotSensor = false, DepartDelaySec = 0, WaitTimeoutSec = 0, IsActive = true, Mode = MissionMode.Schedule };
var scheduleA = new MissionSchedule { Id = Guid.NewGuid(), Name = "A", MissionTypeId = typeA.Id, ZoneId = zone.Id, DaysOfWeek = [1], RunTime = TimeOnly.FromDateTime(day), IsActive = true, CreatedAt = day };
var scheduleB = new MissionSchedule { Id = Guid.NewGuid(), Name = "B", MissionTypeId = typeB.Id, ZoneId = zone.Id, DaysOfWeek = [1], RunTime = TimeOnly.FromDateTime(day), IsActive = true, CreatedAt = day };
Mission Mission(string code, MissionType type, MissionSchedule schedule, MissionStatus status, DateTime created) => new() { Id = Guid.NewGuid(), MissionCode = code, MissionTypeId = type.Id, ZoneId = zone.Id, ScheduleId = schedule.Id, ScheduledFor = created.AddTicks(Math.Abs((long)Guid.NewGuid().GetHashCode())), Status = status, Priority = MissionPriority.Normal, CreatedAt = created };
var onTime = Mission($"M1{suffix}", typeA, scheduleA, MissionStatus.Completed, day); onTime.StartedAt = day; onTime.CompletedAt = day.AddMinutes(30); onTime.DeadlineAt = day.AddMinutes(40);
var late = Mission($"M2{suffix}", typeA, scheduleA, MissionStatus.Completed, day); late.StartedAt = day; late.CompletedAt = day.AddHours(1); late.DeadlineAt = day.AddMinutes(30);
var human = Mission($"M3{suffix}", typeB, scheduleB, MissionStatus.CompletedByHuman, day); human.CompletedAt = day.AddMinutes(20); human.DeadlineAt = day.AddMinutes(30);
var failed = Mission($"M4{suffix}", typeB, scheduleB, MissionStatus.DeliveryFailed, day.AddMinutes(15)); failed.FailReason = MissionFailReason.NoRecipient;
var pending = Mission($"M5{suffix}", typeA, scheduleA, MissionStatus.Pending, day.AddMinutes(45));
db.AddRange(zone, robotType, typeA, typeB, scheduleA, scheduleB, onTime, late, human, failed, pending);
await db.SaveChangesAsync();

var service = new OperationsReportService(db);
var daily = await service.GetAsync(new OperationsReportQuery { From = day.Date, To = day.Date, Floor = zone.Code, GroupBy = "day" });
if (!daily.IsSuccess || daily.Response!.Buckets.Count != 1) throw new InvalidOperationException("Daily report was not returned.");
var bucket = daily.Response.Buckets.Single();
if (bucket.MissionTypes.Single(x => x.Code == typeA.Code).Total != 3 || bucket.MissionTypes.Single(x => x.Code == typeB.Code).Total != 2) throw new InvalidOperationException("Mission type counts are incorrect.");
if (bucket.Outcomes.Completed != 2 || bucket.Outcomes.CompletedByHuman != 1 || bucket.Outcomes.DeliveryFailed != 1 || bucket.Outcomes.Pending != 1) throw new InvalidOperationException("Outcome counts are incorrect.");
if (bucket.OnTime.Eligible != 3 || bucket.OnTime.OnTime != 2 || bucket.OnTime.Rate != 0.6667m || bucket.AverageDeliverySeconds != 2700d) throw new InvalidOperationException("On-time or delivery duration is incorrect.");
if (bucket.FailureReasons.Single().Reason != "NO_RECIPIENT" || bucket.FailureReasons.Single().Count != 1 || bucket.CompletedByHumanCount != 1) throw new InvalidOperationException("Failure/human summary is incorrect.");
var hourly = await service.GetAsync(new OperationsReportQuery { From = day.Date, To = day.Date, Floor = zone.Code, GroupBy = "hour" });
if (!hourly.IsSuccess || hourly.Response!.Buckets.Count != 2) throw new InvalidOperationException("Hourly grouping is incorrect.");
var controller = new ReportsController(new FixedReportService(daily.Response));
var csv = await controller.ExportOperations(new OperationsReportQuery(), "csv", default) as FileContentResult;
var xlsx = await controller.ExportOperations(new OperationsReportQuery(), "xlsx", default) as FileContentResult;
if (csv is null || !System.Text.Encoding.UTF8.GetString(csv.FileContents).Contains("bucket_start") || xlsx is null || xlsx.FileContents.Length < 4 || xlsx.FileContents[0] != (byte)'P' || xlsx.FileContents[1] != (byte)'K')
    throw new InvalidOperationException("Report exports are invalid.");
Console.WriteLine("operations-report=verified day=1 hour=2 floor=1 on-time=2/3 duration=2700 failure=1 human=1 rollback=planned");
await transaction.RollbackAsync();

file sealed class FixedReportService(OperationsReportResponse response) : IOperationsReportService
{
    public Task<OperationsReportResult> GetAsync(OperationsReportQuery query, CancellationToken cancellationToken = default) => Task.FromResult(OperationsReportResult.Success(response));
}
