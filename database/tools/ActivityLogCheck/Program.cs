using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.ActivityLogs.DTOs;
using RobotMissionMS.Business.Features.ActivityLogs.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();

var suffix = Guid.NewGuid().ToString("N")[..8];
var day = DateTime.UtcNow.Date.AddHours(8);
var role = new Role { Id = Guid.NewGuid(), Name = $"L{suffix}", Description = "Activity log rollback role" };
var alpha = new User { Id = Guid.NewGuid(), Username = $"alpha{suffix}", PasswordHash = "test", FullName = "Alpha Search", IsActive = true, RoleId = role.Id, CreatedAt = day };
var beta = new User { Id = Guid.NewGuid(), Username = $"beta{suffix}", PasswordHash = "test", FullName = "Beta User", IsActive = true, RoleId = role.Id, CreatedAt = day };
var at = day.AddMinutes(10);
var first = new ActivityLog { UserId = alpha.Id, Timestamp = at, Action = "TASK_CANCELLED", TargetObject = $"Mission:{suffix}" };
var second = new ActivityLog { UserId = beta.Id, Timestamp = at, Action = "TASK_UPDATED", TargetObject = $"Mission:{suffix}" };
var system = new ActivityLog { Timestamp = day.AddMinutes(20), Action = "SCHEDULE_RUN_LATE", TargetObject = $"ScheduleRun:{suffix}" };
var fullName = new ActivityLog { UserId = alpha.Id, Timestamp = day.AddMinutes(30), Action = "ROBOT_SUSPENDED", TargetObject = $"Robot:{suffix}" };
db.AddRange(role, alpha, beta, first, second, system, fullName);
await db.SaveChangesAsync();

var service = new ActivityLogService(db);
var page = await service.GetAsync(new ActivityLogQuery { From = day.Date, To = day.Date, Page = 1, PageSize = 2 });
if (!page.IsSuccess || page.Response!.Total != 4 || page.Response.Items.Count != 2)
    throw new InvalidOperationException("Pagination result is incorrect.");
var expectedFirstPage = new[] { fullName.Id, system.Id };
if (!page.Response.Items.Select(item => item.Id).SequenceEqual(expectedFirstPage) || page.Response.Items[1].Actor is not null)
    throw new InvalidOperationException("Log ordering or system actor mapping is incorrect.");
var secondPage = await service.GetAsync(new ActivityLogQuery { From = day.Date, To = day.Date, Page = 2, PageSize = 2 });
var expectedSecondPage = new[] { first.Id, second.Id }.OrderByDescending(id => id).ToArray();
if (!secondPage.IsSuccess || !secondPage.Response!.Items.Select(item => item.Id).SequenceEqual(expectedSecondPage))
    throw new InvalidOperationException("Same-timestamp ordering is not stable.");

var sameTime = await service.GetAsync(new ActivityLogQuery { From = day.Date, To = day.Date, Action = "task_cancelled", PageSize = 20 });
if (!sameTime.IsSuccess || sameTime.Response!.Total != 1 || sameTime.Response.Items.Single().Actor?.Username != alpha.Username)
    throw new InvalidOperationException("Action filter or actor projection is incorrect.");

var freeText = await service.GetAsync(new ActivityLogQuery { From = day.Date, To = day.Date, Q = "alpha search", PageSize = 20 });
if (!freeText.IsSuccess || freeText.Response!.Total != 2 || freeText.Response.Items.Any(item => item.Actor?.Id != alpha.Id))
    throw new InvalidOperationException("Free-text actor search is incorrect.");

var actor = await service.GetAsync(new ActivityLogQuery { From = day.Date, To = day.Date, Actor = beta.Id, PageSize = 20 });
if (!actor.IsSuccess || actor.Response!.Total != 1 || actor.Response.Items.Single().Action != "TASK_UPDATED")
    throw new InvalidOperationException("Actor filter is incorrect.");

var exported = await service.ExportAsync(new ActivityLogQuery { From = day.Date, To = day.Date });
if (!exported.IsSuccess || exported.Items!.Count != 4 || exported.Items.Single(item => item.Id == system.Id).Message != $"SCHEDULE_RUN_LATE – ScheduleRun:{suffix}")
    throw new InvalidOperationException("Export rows or generated message are incorrect.");

db.ActivityLogs.AddRange(Enumerable.Range(0, 50_001).Select(index => new ActivityLog
{
    Timestamp = day.AddHours(2).AddTicks(index), Action = "EXPORT_LIMIT_FIXTURE", TargetObject = $"Fixture:{index}"
}));
await db.SaveChangesAsync();
var limited = await service.ExportAsync(new ActivityLogQuery { From = day.Date, To = day.Date });
if (limited.Error != ActivityLogQueryError.ExportLimitExceeded)
    throw new InvalidOperationException("Export limit was not enforced.");

Console.WriteLine("activity-log=verified filters=actor-action-text pagination=stable system-actor=null export=50000-limit rollback=planned");
await transaction.RollbackAsync();
