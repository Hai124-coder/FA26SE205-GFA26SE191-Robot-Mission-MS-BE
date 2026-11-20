using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Incidents.DTOs;
using RobotMissionMS.Business.Features.Incidents.Services;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("SECTION9_CONNECTION is required and must not be printed.");

await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);
var actorUserId = await db.Users.AsNoTracking().OrderBy(user => user.Username).Select(user => user.Id).FirstOrDefaultAsync();
if (actorUserId == Guid.Empty) throw new InvalidOperationException("Incident command check requires one existing user.");

await using var transaction = await db.Database.BeginTransactionAsync();
try
{
    var service = new IncidentCommandService(db, TimeProvider.System);
    var created = await service.CreateAsync(actorUserId, new CreateIncidentRequest { ScenarioCode = "E1", Message = "Rollback-only incident command verification." });
    if (!created.IsSuccess || created.Response is null || created.Response.Phase != "DECIDE") throw new InvalidOperationException($"Create failed: {created.Error}");

    var resolved = await service.ApplyActionAsync(created.Response.AlertId, actorUserId, new ApplyIncidentActionRequest { Action = "NOTIFY", Note = "Verified then rolled back." });
    if (!resolved.IsSuccess || resolved.Response is null || resolved.Response.Status != "RESOLVED" || resolved.Response.Phase != "DONE") throw new InvalidOperationException($"Action failed: {resolved.Error}");

    var events = await db.AlertTimelineEntries.CountAsync(entry => entry.AlertId == created.Response.AlertId);
    var alert = await db.Alerts.SingleAsync(item => item.Id == created.Response.AlertId);
    if (events != 5 || alert.Status != AlertStatus.Resolved || alert.Phase != IncidentPhase.Done || alert.ResolvedByUserId != actorUserId)
        throw new InvalidOperationException($"Unexpected persisted workflow: events={events}, status={alert.Status}, phase={alert.Phase}.");

    Console.WriteLine("incident-command=verified create-e1-notify-resolve timeline=5 rollback=planned");
}
finally
{
    await transaction.RollbackAsync();
}
