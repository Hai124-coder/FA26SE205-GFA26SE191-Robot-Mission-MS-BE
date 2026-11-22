using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Incidents.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Incidents.Services;

public sealed class IncidentReadService(RobotMissionDbContext db) : IIncidentReadService
{
    public async Task<IncidentListResult> GetIncidentsAsync(IncidentListQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Valid(query) || !TryEnums(query.Status, out IReadOnlyList<AlertStatus>? statuses) || !TryEnums(query.Category, out IReadOnlyList<AlertCategory>? categories))
            return IncidentListResult.Failure(IncidentReadError.InvalidQuery);
        var floor = operatorFloor ?? Text(query.Floor);
        var alerts = Filter(db.Alerts.AsNoTracking(), query.Type, query.RobotId, query.From, query.To, floor, statuses, categories);
        var total = await alerts.CountAsync(cancellationToken);
        var rows = await alerts.Include(alert => alert.Zone).Include(alert => alert.Robot).Include(alert => alert.Trip)
            .OrderByDescending(alert => alert.CreatedAt).ThenBy(alert => alert.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return IncidentListResult.Success(new PagedResponse<IncidentListItemResponse> { Items = rows.Select(ToListItem).ToList(), Page = query.Page, PageSize = query.PageSize, Total = total });
    }

    public async Task<IncidentDetailResult> GetIncidentAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) return IncidentDetailResult.Failure(IncidentReadError.NotFound);
        var alert = await db.Alerts.AsNoTracking().Include(value => value.Zone).Include(value => value.Robot).Include(value => value.Trip)
            .Include(value => value.Mission).Include(value => value.ResolvedByUser)
            .Include(value => value.TimelineEntries).ThenInclude(value => value.ActorUser)
            .Include(value => value.AlertMissions).ThenInclude(value => value.Mission)
            .AsSplitQuery().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (alert is null) return IncidentDetailResult.Failure(IncidentReadError.NotFound);
        if (operatorFloor is not null && alert.Zone?.Code is { } floor && !string.Equals(floor, operatorFloor, StringComparison.OrdinalIgnoreCase)) return IncidentDetailResult.Failure(IncidentReadError.FloorForbidden);
        var missions = alert.AlertMissions.Select(link => link.Mission).Append(alert.Mission).Where(mission => mission is not null).Cast<Mission>().GroupBy(mission => mission.Id).Select(group => group.First()).OrderBy(mission => mission.MissionCode)
            .Select(mission => new IncidentMissionResponse { Id = mission.Id, MissionCode = mission.MissionCode, Status = Code(mission.Status) }).ToList();
        return IncidentDetailResult.Success(new IncidentDetailResponse
        {
            Id = alert.Id, ScenarioCode = alert.ScenarioCode, Type = alert.AlertType, Category = Code(alert.Category), Severity = Code(alert.Severity), Status = Code(alert.Status), Phase = Code(alert.Phase), CargoState = Code(alert.CargoState),
            Floor = alert.Zone?.Code, RobotId = alert.RobotId, RobotCode = alert.Robot?.RobotCode, TripId = alert.TripId, TripCode = alert.Trip?.TripCode, Message = alert.Message, CreatedAt = Offset(alert.CreatedAt), ResolvedAt = Offset(alert.ResolvedAt),
            SuggestedAction = Code(alert.SuggestedAction), ResolutionNote = alert.ResolutionNote, ResolvedBy = alert.ResolvedByUser?.FullName, AllowedActions = AllowedActions(alert), Missions = missions,
            Timeline = alert.TimelineEntries.OrderBy(value => value.OccurredAt).ThenBy(value => value.Id).Select(value => new IncidentTimelineItemResponse { Id = value.Id, EventType = Code(value.EventType), Action = Code(value.Action), FromPhase = Code(value.FromPhase), ToPhase = Code(value.ToPhase), Actor = value.ActorUser?.FullName, Note = value.Note, OccurredAt = Offset(value.OccurredAt) }).ToList()
        });
    }

    public async Task<IncidentStatsResult> GetStatsAsync(IncidentStatsQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Valid(query)) return IncidentStatsResult.Failure(IncidentReadError.InvalidQuery);
        var floor = operatorFloor ?? Text(query.Floor);
        var alerts = Filter(db.Alerts.AsNoTracking(), null, null, query.From, query.To, floor, null, null);
        var openCount = await alerts.CountAsync(alert => alert.Status != AlertStatus.Resolved, cancellationToken);
        var resolved = await alerts.Where(alert => alert.Status == AlertStatus.Resolved).Select(alert => new { alert.Id, alert.CreatedAt, alert.ResolvedAt, alert.ResolvedByUserId }).ToListAsync(cancellationToken);
        var ids = resolved.Select(value => value.Id).ToArray();
        var humanCompletedMissionCount = ids.Length == 0 ? 0 : await db.AlertMissions.AsNoTracking().Where(link => ids.Contains(link.AlertId) && link.Mission.Status == MissionStatus.CompletedByHuman).Select(link => link.MissionId).Distinct().CountAsync(cancellationToken);
        var durations = resolved.Where(value => value.ResolvedAt is not null).Select(value => (value.ResolvedAt!.Value - value.CreatedAt).TotalMinutes).ToArray();
        return IncidentStatsResult.Success(new IncidentStatsResponse
        {
            OpenCount = openCount, ResolvedCount = resolved.Count, ResolvedByOperatorCount = resolved.Count(value => value.ResolvedByUserId is not null), AutoResolvedCount = resolved.Count(value => value.ResolvedByUserId is null),
            AverageResolutionMinutes = durations.Length == 0 ? null : Math.Round(durations.Average(), 2), HumanCompletedMissionCount = humanCompletedMissionCount
        });
    }

    private static IQueryable<Alert> Filter(IQueryable<Alert> query, string? type, Guid? robotId, DateTime? from, DateTime? to, string? floor, IReadOnlyList<AlertStatus>? statuses, IReadOnlyList<AlertCategory>? categories)
    {
        if (floor is not null) query = query.Where(alert => alert.ZoneId == null || (alert.Zone != null && alert.Zone.Code == floor));
        if (statuses is not null) query = query.Where(alert => statuses.Contains(alert.Status));
        if (categories is not null) query = query.Where(alert => categories.Contains(alert.Category));
        if (Text(type) is { } alertType) query = query.Where(alert => alert.AlertType == alertType);
        if (robotId is { } robot) query = query.Where(alert => alert.RobotId == robot);
        if (from is { } start) query = query.Where(alert => alert.CreatedAt >= start);
        if (to is { } end) query = query.Where(alert => alert.CreatedAt < end.AddDays(1));
        return query;
    }

    private static IReadOnlyList<string> AllowedActions(Alert alert)
    {
        if (alert.Status == AlertStatus.Resolved || alert.ScenarioCode is null || !IncidentContract.Rules.TryGetValue(alert.ScenarioCode, out var rule)) return [];
        if (alert.Phase == IncidentPhase.Human) return [ActionCode(IncidentAction.HumanDone)];
        if (alert.Phase != IncidentPhase.Decide) return [];
        var values = rule.AllowedActions.Select(ActionCode).ToList();
        if (alert.SuggestedAction is not null) values.Insert(0, ActionCode(IncidentAction.Accept));
        return values.Distinct(StringComparer.Ordinal).ToList();
    }
    private static bool Valid(object query) => Validator.TryValidateObject(query, new ValidationContext(query), null, true) && query switch { IncidentListQuery value => !value.From.HasValue || !value.To.HasValue || value.From <= value.To, IncidentStatsQuery value => !value.From.HasValue || !value.To.HasValue || value.From <= value.To, _ => false };
    private static bool TryEnums<T>(string? input, out IReadOnlyList<T>? values) where T : struct, Enum
    {
        values = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        var parsed = new List<T>();
        foreach (var text in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (!Enum.TryParse<T>(text, true, out var value) || !Enum.IsDefined(value)) return false; else parsed.Add(value);
        if (parsed.Count == 0) return false;
        values = parsed.Distinct().ToList();
        return true;
    }
    private static IncidentListItemResponse ToListItem(Alert alert) => new() { Id = alert.Id, ScenarioCode = alert.ScenarioCode, Type = alert.AlertType, Category = Code(alert.Category), Severity = Code(alert.Severity), Status = Code(alert.Status), Phase = Code(alert.Phase), CargoState = Code(alert.CargoState), Floor = alert.Zone?.Code, RobotId = alert.RobotId, RobotCode = alert.Robot?.RobotCode, TripId = alert.TripId, TripCode = alert.Trip?.TripCode, Message = alert.Message, CreatedAt = Offset(alert.CreatedAt), ResolvedAt = Offset(alert.ResolvedAt) };
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset Offset(DateTime value) => new(value);
    private static DateTimeOffset? Offset(DateTime? value) => value is null ? null : new DateTimeOffset(value.Value);
    private static string? Code<T>(T? value) where T : struct, Enum => value is null ? null : Code(value.Value);
    private static string Code<T>(T value) where T : struct, Enum => value is IncidentAction action ? ActionCode(action) : value.ToString().ToUpperInvariant();
    private static string ActionCode(IncidentAction action) => action switch
    {
        IncidentAction.GoOn => "GO_ON", IncidentAction.WaitMore => "WAIT_MORE", IncidentAction.GoPartial => "GO_PARTIAL", IncidentAction.HumanDone => "HUMAN_DONE", IncidentAction.WaitConn => "WAIT_CONN", IncidentAction.AsFault => "AS_FAULT", IncidentAction.BrokerUp => "BROKER_UP", _ => action.ToString().ToUpperInvariant()
    };
}
