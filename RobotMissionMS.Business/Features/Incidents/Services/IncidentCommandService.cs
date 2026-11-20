using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Incidents.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Incidents.Services;

public sealed class IncidentCommandService(RobotMissionDbContext db, TimeProvider? timeProvider = null) : IIncidentCommandService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<IncidentCommandResult> CreateAsync(Guid? actorUserId, CreateIncidentRequest request, CancellationToken cancellationToken = default)
    {
        if (!Valid(request) || !IncidentContract.Rules.TryGetValue(request.ScenarioCode!.Trim(), out var rule) || !ValidIds(request.AffectedMissionIds))
            return IncidentCommandResult.Failure(IncidentCommandError.InvalidRequest);

        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (!await ActorExistsAsync(actorUserId, cancellationToken)) return IncidentCommandResult.Failure(IncidentCommandError.ActorNotFound);
        if (!await RelatedEntitiesExistAsync(request.ZoneId, request.RobotId, request.MissionId, request.TripId, cancellationToken))
            return IncidentCommandResult.Failure(IncidentCommandError.RelatedEntityNotFound);

        var affectedMissionIds = MissionIds(request.MissionId, request.AffectedMissionIds);
        if (!await MissionsExistAsync(affectedMissionIds, cancellationToken)) return IncidentCommandResult.Failure(IncidentCommandError.RelatedMissionNotFound);

        var now = clock.GetUtcNow().UtcDateTime;
        var alert = new Alert
        {
            Id = Guid.NewGuid(), AlertType = rule.AlertType, Category = rule.Category, Severity = rule.Severity,
            Status = AlertStatus.Open, ScenarioCode = rule.ScenarioCode, Phase = rule.InitialPhase,
            CargoState = rule.CargoState, SuggestedAction = rule.SuggestedAction, Message = request.Message!.Trim(),
            ZoneId = request.ZoneId, RobotId = request.RobotId, MissionId = request.MissionId, TripId = request.TripId, CreatedAt = now
        };
        db.Alerts.Add(alert);
        AddTimeline(alert.Id, IncidentTimelineEventType.Detected, actorUserId, now, null, null, rule.InitialPhase, "Incident detected.");
        if (rule.SuggestedAction is { } suggested)
            AddTimeline(alert.Id, IncidentTimelineEventType.Suggested, null, now, suggested, null, null, "Suggested action generated from the incident contract.");
        await AddMissionLinksAsync(alert.Id, affectedMissionIds, now, cancellationToken);
        AddActivity(actorUserId, now, "INCIDENT_CREATED", alert.Id);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return IncidentCommandResult.Success(Response(alert));
    }

    public async Task<IncidentCommandResult> ApplyActionAsync(Guid alertId, Guid actorUserId, string floor, ApplyIncidentActionRequest request, CancellationToken cancellationToken = default)
    {
        if (alertId == Guid.Empty || actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(floor) || !Valid(request) || !TryAction(request.Action, out var requestedAction) || !ValidIds(request.AffectedMissionIds) || !ValidSupplementalFields(requestedAction, request))
            return IncidentCommandResult.Failure(IncidentCommandError.InvalidRequest);

        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (!await ActorExistsAsync(actorUserId, cancellationToken)) return IncidentCommandResult.Failure(IncidentCommandError.ActorNotFound);
        var alert = await db.Alerts.FromSqlInterpolated($"SELECT * FROM public.\"Alerts\" WHERE \"Id\" = {alertId} FOR UPDATE").Include(value => value.Zone).SingleOrDefaultAsync(cancellationToken);
        if (alert is null) return IncidentCommandResult.Failure(IncidentCommandError.AlertNotFound);
        if (alert.Zone?.Code is { } incidentFloor && !string.Equals(incidentFloor, floor.Trim(), StringComparison.OrdinalIgnoreCase)) return IncidentCommandResult.Failure(IncidentCommandError.FloorForbidden);
        if (alert.Status == AlertStatus.Resolved) return IncidentCommandResult.Failure(IncidentCommandError.AlertResolved);
        if (alert.ScenarioCode is null || !IncidentContract.Rules.TryGetValue(alert.ScenarioCode, out var rule)) return IncidentCommandResult.Failure(IncidentCommandError.ScenarioNotSupported);
        var action = requestedAction == IncidentAction.Accept ? alert.SuggestedAction : requestedAction;
        if (action is null || !CanApply(rule, alert.Phase, action.Value)) return IncidentCommandResult.Failure(alert.Phase == IncidentPhase.Human ? IncidentCommandError.InvalidPhase : IncidentCommandError.ActionNotAllowed);

        var affectedMissionIds = MissionIds(alert.MissionId, request.AffectedMissionIds);
        if (!await MissionsExistAsync(affectedMissionIds, cancellationToken)) return IncidentCommandResult.Failure(IncidentCommandError.RelatedMissionNotFound);

        var now = clock.GetUtcNow().UtcDateTime;
        var nextPhase = NextPhase(alert.Phase, action.Value);
        var payloadJson = JsonSerializer.Serialize(new { assignee = TrimNote(request.Assignee), after = request.After?.Trim().ToUpperInvariant(), targetRobotId = request.TargetRobotId, resolvedAction = ActionCode(action.Value) });
        AddTimeline(alert.Id, IncidentTimelineEventType.ActionRequested, actorUserId, now, requestedAction, null, null, TrimNote(request.Note), payloadJson);
        if (nextPhase != alert.Phase)
        {
            AddTimeline(alert.Id, IncidentTimelineEventType.PhaseChanged, actorUserId, now, null, alert.Phase, nextPhase, null);
            alert.Phase = nextPhase;
        }
        if (nextPhase == IncidentPhase.Done)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedByUserId = actorUserId;
            alert.ResolvedAt = now;
            alert.ResolutionNote = TrimNote(request.Note);
            AddTimeline(alert.Id, IncidentTimelineEventType.Resolved, actorUserId, now, requestedAction, null, IncidentPhase.Done, alert.ResolutionNote, payloadJson);
        }
        await AddMissionLinksAsync(alert.Id, affectedMissionIds, now, cancellationToken);
        AddActivity(actorUserId, now, $"INCIDENT_ACTION_{ActionCode(requestedAction)}", alert.Id);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return IncidentCommandResult.Success(Response(alert));
    }

    private async Task<bool> ActorExistsAsync(Guid? actorUserId, CancellationToken ct) => actorUserId is null || await db.Users.AsNoTracking().AnyAsync(user => user.Id == actorUserId.Value, ct);
    private async Task<bool> RelatedEntitiesExistAsync(Guid? zoneId, Guid? robotId, Guid? missionId, Guid? tripId, CancellationToken ct)
    {
        if (zoneId is { } zone && !await db.Zones.AsNoTracking().AnyAsync(value => value.Id == zone, ct)) return false;
        if (robotId is { } robot && !await db.Robots.AsNoTracking().AnyAsync(value => value.Id == robot, ct)) return false;
        if (missionId is { } mission && !await db.Missions.AsNoTracking().AnyAsync(value => value.Id == mission, ct)) return false;
        return tripId is not { } trip || await db.Trips.AsNoTracking().AnyAsync(value => value.Id == trip, ct);
    }
    private async Task<bool> MissionsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => ids.Count == 0 || await db.Missions.AsNoTracking().CountAsync(mission => ids.Contains(mission.Id), ct) == ids.Count;
    private void AddTimeline(Guid alertId, IncidentTimelineEventType eventType, Guid? actorUserId, DateTime now, IncidentAction? action, IncidentPhase? fromPhase, IncidentPhase? toPhase, string? note, string? payloadJson = null) => db.AlertTimelineEntries.Add(new AlertTimelineEntry { Id = Guid.NewGuid(), AlertId = alertId, EventType = eventType, ActorUserId = actorUserId, OccurredAt = now, Action = action, FromPhase = fromPhase, ToPhase = toPhase, Note = note, PayloadJson = payloadJson });
    private async Task AddMissionLinksAsync(Guid alertId, IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var existing = await db.AlertMissions.AsNoTracking().Where(link => link.AlertId == alertId && ids.Contains(link.MissionId)).Select(link => link.MissionId).ToListAsync(ct);
        foreach (var id in ids.Except(existing)) db.AlertMissions.Add(new AlertMission { AlertId = alertId, MissionId = id, CreatedAt = now });
    }
    private void AddActivity(Guid? actorUserId, DateTime now, string action, Guid alertId) => db.ActivityLogs.Add(new ActivityLog { UserId = actorUserId, Timestamp = now, Action = action, TargetObject = $"Alert:{alertId:D}" });
    private static bool CanApply(IncidentRule rule, IncidentPhase? phase, IncidentAction action) => phase == IncidentPhase.Human ? action == IncidentAction.HumanDone : phase == IncidentPhase.Decide && rule.AllowedActions.Contains(action);
    private static IncidentPhase? NextPhase(IncidentPhase? current, IncidentAction action) => action switch
    {
        IncidentAction.Human or IncidentAction.Reassign or IncidentAction.Requeue or IncidentAction.AsFault => IncidentPhase.Human,
        IncidentAction.HumanDone or IncidentAction.GoOn or IncidentAction.GoPartial or IncidentAction.Resume or IncidentAction.Notify or IncidentAction.BrokerUp or IncidentAction.Accept => IncidentPhase.Done,
        _ => current
    };
    private static IReadOnlyCollection<Guid> MissionIds(Guid? primary, IReadOnlyCollection<Guid>? additional) => (additional ?? []).Append(primary ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
    private static bool ValidIds(IReadOnlyCollection<Guid>? ids) => ids is null || ids.All(id => id != Guid.Empty);
    private static bool Valid(object request) => Validator.TryValidateObject(request, new ValidationContext(request), null, true);
    private static bool ValidSupplementalFields(IncidentAction action, ApplyIncidentActionRequest request)
    {
        if (action == IncidentAction.Human && string.IsNullOrWhiteSpace(request.Assignee)) return false;
        return string.IsNullOrWhiteSpace(request.After) || (TryAction(request.After, out var after) && after is IncidentAction.Maintenance or IncidentAction.Charge);
    }
    private static bool TryAction(string? text, out IncidentAction action)
    {
        action = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalized, true, out action) && Enum.IsDefined(action);
    }
    private static string? TrimNote(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string ActionCode(IncidentAction action) => action switch
    {
        IncidentAction.GoOn => "GO_ON", IncidentAction.WaitMore => "WAIT_MORE", IncidentAction.GoPartial => "GO_PARTIAL",
        IncidentAction.HumanDone => "HUMAN_DONE", IncidentAction.WaitConn => "WAIT_CONN", IncidentAction.AsFault => "AS_FAULT",
        IncidentAction.BrokerUp => "BROKER_UP", _ => action.ToString().ToUpperInvariant()
    };
    private static IncidentActionResponse Response(Alert alert) => new() { AlertId = alert.Id, Status = alert.Status.ToString().ToUpperInvariant(), Phase = alert.Phase?.ToString().ToUpperInvariant(), SuggestedAction = alert.SuggestedAction is { } action ? ActionCode(action) : null };
}
