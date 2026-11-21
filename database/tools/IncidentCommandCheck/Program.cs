using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Incidents.DTOs;
using RobotMissionMS.Business.Features.Incidents.Services;
using RobotMissionMS.DataAccess.Entities;
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

    var resolved = await service.ApplyActionAsync(created.Response.AlertId, actorUserId, "TEST", new ApplyIncidentActionRequest { Action = "NOTIFY", Note = "Verified then rolled back." });
    if (!resolved.IsSuccess || resolved.Response is null || resolved.Response.Status != "RESOLVED" || resolved.Response.Phase != "DONE") throw new InvalidOperationException($"Action failed: {resolved.Error}");

    var events = await db.AlertTimelineEntries.CountAsync(entry => entry.AlertId == created.Response.AlertId);
    var alert = await db.Alerts.SingleAsync(item => item.Id == created.Response.AlertId);
    if (events != 5 || alert.Status != AlertStatus.Resolved || alert.Phase != IncidentPhase.Done || alert.ResolvedByUserId != actorUserId)
        throw new InvalidOperationException($"Unexpected persisted workflow: events={events}, status={alert.Status}, phase={alert.Phase}.");

    var zone = await db.Zones.AsNoTracking().OrderBy(value => value.Code).FirstOrDefaultAsync();
    var missionType = await db.MissionTypes.AsNoTracking().OrderBy(value => value.Code).FirstOrDefaultAsync();
    if (zone is null || missionType is null) throw new InvalidOperationException("Incident command effect check requires one zone and one mission type.");

    var now = DateTime.UtcNow;
    var schedule = new MissionSchedule { Id = Guid.NewGuid(), Name = $"Rollback check {Guid.NewGuid():N}", MissionTypeId = missionType.Id, ZoneId = zone.Id, DaysOfWeek = [1], RunTime = TimeOnly.FromTimeSpan(now.TimeOfDay), IsActive = false, CreatedByUserId = actorUserId, CreatedAt = now };
    var requeueMission = NewMission(schedule, missionType, zone, now, MissionStatus.Assigned);
    var humanMission = NewMission(schedule, missionType, zone, now.AddSeconds(1), MissionStatus.InProgress);
    var absentMission = NewMission(schedule, missionType, zone, now.AddSeconds(2), MissionStatus.InProgress);
    var partialMission = NewMission(schedule, missionType, zone, now.AddSeconds(3), MissionStatus.Assigned);
    db.MissionSchedules.Add(schedule);
    db.Missions.AddRange(requeueMission, humanMission, absentMission, partialMission);
    await db.SaveChangesAsync();

    var requeueIncident = await service.CreateAsync(actorUserId, new CreateIncidentRequest { ScenarioCode = "B1", Message = "Rollback-only requeue verification.", ZoneId = zone.Id, MissionId = requeueMission.Id });
    if (!requeueIncident.IsSuccess || requeueIncident.Response is null) throw new InvalidOperationException($"B1 create failed: {requeueIncident.Error}");
    var requeued = await service.ApplyActionAsync(requeueIncident.Response.AlertId, actorUserId, zone.Code, new ApplyIncidentActionRequest { Action = "REQUEUE" });
    if (!requeued.IsSuccess || requeued.Response?.Status != "RESOLVED") throw new InvalidOperationException($"REQUEUE failed: {requeued.Error}");
    if ((await db.Missions.SingleAsync(value => value.Id == requeueMission.Id)).Status != MissionStatus.Rescheduled) throw new InvalidOperationException("REQUEUE did not reschedule the mission.");

    var humanIncident = await service.CreateAsync(actorUserId, new CreateIncidentRequest { ScenarioCode = "B2", Message = "Rollback-only human completion verification.", ZoneId = zone.Id, MissionId = humanMission.Id });
    if (!humanIncident.IsSuccess || humanIncident.Response is null) throw new InvalidOperationException($"B2 create failed: {humanIncident.Error}");
    var handedOver = await service.ApplyActionAsync(humanIncident.Response.AlertId, actorUserId, zone.Code, new ApplyIncidentActionRequest { Action = "HUMAN", Assignee = "Rollback checker" });
    if (!handedOver.IsSuccess || handedOver.Response?.Phase != "HUMAN") throw new InvalidOperationException($"HUMAN failed: {handedOver.Error}");
    var humanDone = await service.ApplyActionAsync(humanIncident.Response.AlertId, actorUserId, zone.Code, new ApplyIncidentActionRequest { Action = "HUMAN_DONE", After = "MAINTENANCE" });
    if (!humanDone.IsSuccess || humanDone.Response?.Status != "RESOLVED") throw new InvalidOperationException($"HUMAN_DONE failed: {humanDone.Error}");
    var completedMission = await db.Missions.SingleAsync(value => value.Id == humanMission.Id);
    var histories = await db.MissionStatusHistories.CountAsync(value => value.MissionId == humanMission.Id && value.ToStatus == MissionStatus.CompletedByHuman);
    if (completedMission.Status != MissionStatus.CompletedByHuman || completedMission.CompletedAt is null || histories != 1) throw new InvalidOperationException("HUMAN_DONE did not complete and audit the mission.");

    var absentIncident = await service.CreateAsync(actorUserId, new CreateIncidentRequest { ScenarioCode = "A1", Message = "Rollback-only recipient absent verification.", ZoneId = zone.Id, MissionId = absentMission.Id });
    if (!absentIncident.IsSuccess || absentIncident.Response is null) throw new InvalidOperationException($"A1 create failed: {absentIncident.Error}");
    var goneOn = await service.ApplyActionAsync(absentIncident.Response.AlertId, actorUserId, zone.Code, new ApplyIncidentActionRequest { Action = "GO_ON" });
    if (!goneOn.IsSuccess || goneOn.Response?.Status != "RESOLVED" || (await db.Missions.SingleAsync(value => value.Id == absentMission.Id)).Status != MissionStatus.DeliveryFailed) throw new InvalidOperationException($"GO_ON failed: {goneOn.Error}");

    var partialIncident = await service.CreateAsync(actorUserId, new CreateIncidentRequest { ScenarioCode = "A5", Message = "Rollback-only partial pickup verification.", ZoneId = zone.Id, MissionId = partialMission.Id });
    if (!partialIncident.IsSuccess || partialIncident.Response is null) throw new InvalidOperationException($"A5 create failed: {partialIncident.Error}");
    var partial = await service.ApplyActionAsync(partialIncident.Response.AlertId, actorUserId, zone.Code, new ApplyIncidentActionRequest { Action = "GO_PARTIAL" });
    if (!partial.IsSuccess || partial.Response?.Status != "RESOLVED" || (await db.Missions.SingleAsync(value => value.Id == partialMission.Id)).Status != MissionStatus.Rescheduled) throw new InvalidOperationException($"GO_PARTIAL failed: {partial.Error}");

    Console.WriteLine("incident-command=verified notify,requeue,human-done,go-on,go-partial rollback=planned");
}

finally
{
    await transaction.RollbackAsync();
}

static Mission NewMission(MissionSchedule schedule, MissionType missionType, Zone zone, DateTime scheduledFor, MissionStatus status) => new()
{
    Id = Guid.NewGuid(), MissionCode = $"CHK-{Guid.NewGuid():N}"[..36].ToUpperInvariant(), MissionTypeId = missionType.Id,
    ZoneId = zone.Id, ScheduleId = schedule.Id, ScheduledFor = scheduledFor, Status = status,
    Priority = MissionPriority.Normal, CreatedAt = scheduledFor
};
