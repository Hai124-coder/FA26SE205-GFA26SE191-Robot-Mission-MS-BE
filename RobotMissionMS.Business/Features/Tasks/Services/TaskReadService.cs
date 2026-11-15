using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Tasks.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Tasks.Services;

public sealed class TaskReadService(RobotMissionDbContext db, TimeProvider? timeProvider = null) : ITaskReadService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    public async Task<TaskListResult> GetTasksAsync(TaskListQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!IsValid(query) || !TryParseStatuses(query.Status, out var statuses)) return TaskListResult.Failure(TaskReadError.InvalidQuery);
        var floor = operatorFloor ?? Normalize(query.Floor);
        var missions = Filter(db.Missions.AsNoTracking(), query, floor, statuses);
        var total = await missions.CountAsync(cancellationToken);
        var entities = await missions.Include(m => m.MissionType).Include(m => m.Zone).Include(m => m.ToLocation).Include(m => m.Trip).ThenInclude(t => t!.Robot)
            .OrderByDescending(m => m.CreatedAt).ThenBy(m => m.MissionCode).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return TaskListResult.Success(new PagedResponse<TaskListItemResponse> { Items = entities.Select(ToListItem).ToList(), Page = query.Page, PageSize = query.PageSize, Total = total });
    }

    public async Task<TaskQueueResult> GetQueueAsync(TaskQueueQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true)) return TaskQueueResult.Failure(TaskReadError.InvalidQuery);
        var floor = operatorFloor ?? Normalize(query.Floor);
        var settings = await db.SystemSettings.AsNoTracking().Where(setting => QueueSettingKeys.All.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal, cancellationToken);
        if (!TryReadQueueSettings(settings, out var config)) return TaskQueueResult.Failure(TaskReadError.InvalidQuery);
        var statuses = new[] { MissionStatus.Pending, MissionStatus.Rescheduled };
        var missions = db.Missions.AsNoTracking().Include(m => m.Zone).Include(m => m.MissionType).Include(m => m.ToLocation)
            .Where(m => statuses.Contains(m.Status));
        if (floor is not null) missions = missions.Where(m => m.Zone.Code == floor);
        if (Normalize(query.Type) is { } type) missions = missions.Where(m => m.MissionType.Code == type);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await missions.ToListAsync(cancellationToken);
        return TaskQueueResult.Success(rows.Select(m => ToQueueItem(m, config, now)).OrderByDescending(item => item.PriorityScore).ThenBy(item => item.CreatedAt).ThenBy(item => item.MissionCode).ToList());
    }

    public async Task<TaskDetailResult> GetTaskAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        var mission = await LoadDetail(id, cancellationToken);
        if (mission is null) return TaskDetailResult.Failure(TaskReadError.NotFound);
        if (operatorFloor is not null && mission.Zone.Code != operatorFloor) return TaskDetailResult.Failure(TaskReadError.FloorForbidden);
        var alerts = mission.Alerts.OrderByDescending(a => a.CreatedAt).Select(a => new TaskAlertResponse { Id = a.Id, Type = a.AlertType, Status = Code(a.Status), Severity = Code(a.Severity), Message = a.Message, CreatedAt = new DateTimeOffset(a.CreatedAt) }).ToList();
        var actions = mission.Status is MissionStatus.Pending or MissionStatus.Rescheduled ? new[] { "EDIT", "CANCEL" } : Array.Empty<string>();
        return TaskDetailResult.Success(new TaskDetailResponse
        {
            Id = mission.Id, MissionCode = mission.MissionCode, RequestCode = mission.RequestCode, Status = Code(mission.Status), Priority = Code(mission.Priority), MissionType = mission.MissionType.Code,
            Floor = mission.Zone.Code, Department = mission.ToLocation?.Code, ItemCode = mission.ItemCodeSnapshot, ItemName = mission.ItemNameSnapshot, Quantity = mission.Quantity,
            CreatedAt = new DateTimeOffset(mission.CreatedAt), DeadlineAt = Offset(mission.DeadlineAt), RobotCode = mission.Trip?.Robot?.RobotCode, TripCode = mission.Trip?.TripCode,
            FromLocation = mission.FromLocation?.Code, ToLocation = mission.ToLocation?.Code, KgPerUnit = mission.KgPerUnitSnapshot, WeightKg = mission.WeightKgSnapshot,
            SlotNo = mission.SlotNo, Note = mission.Note, FailReason = mission.FailReason is null ? null : Code(mission.FailReason.Value), FailNote = mission.FailNote,
            FailureCount = mission.MissionStatusHistories.Count(h => h.ToStatus == MissionStatus.DeliveryFailed), Alerts = alerts, Actions = actions
        });
    }

    public async Task<TaskTimelineResult> GetTimelineAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        var mission = await db.Missions.AsNoTracking().Include(m => m.Zone).Include(m => m.CreatedByUser).Include(m => m.MissionStatusHistories).ThenInclude(h => h.ChangedByUser)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (mission is null) return TaskTimelineResult.Failure(TaskReadError.NotFound);
        if (operatorFloor is not null && mission.Zone.Code != operatorFloor) return TaskTimelineResult.Failure(TaskReadError.FloorForbidden);
        var items = new List<TaskTimelineItemResponse> { new() { Event = "CREATED", OccurredAt = new DateTimeOffset(mission.CreatedAt), ToStatus = Code(MissionStatus.Pending), Actor = mission.CreatedByUser?.FullName } };
        items.AddRange(mission.MissionStatusHistories.OrderBy(h => h.ChangedAt).Select(h => new TaskTimelineItemResponse { Event = "STATUS_CHANGED", OccurredAt = new DateTimeOffset(h.ChangedAt), FromStatus = h.FromStatus is null ? null : Code(h.FromStatus.Value), ToStatus = Code(h.ToStatus), Actor = h.ChangedByUser?.FullName, Reason = h.Reason }));
        return TaskTimelineResult.Success(new TaskTimelineResponse { TaskId = mission.Id, Items = items.OrderBy(x => x.OccurredAt).ToList() });
    }

    private async Task<Mission?> LoadDetail(Guid id, CancellationToken ct) => await db.Missions.AsNoTracking().Include(m => m.MissionType).Include(m => m.Zone).Include(m => m.FromLocation).Include(m => m.ToLocation)
        .Include(m => m.Trip).ThenInclude(t => t!.Robot).Include(m => m.Alerts).Include(m => m.MissionStatusHistories).SingleOrDefaultAsync(m => m.Id == id, ct);

    private static IQueryable<Mission> Filter(IQueryable<Mission> query, TaskListQuery q, string? floor, IReadOnlyList<MissionStatus>? statuses)
    {
        if (floor is not null) query = query.Where(m => m.Zone.Code == floor);
        if (statuses is not null) query = query.Where(m => statuses.Contains(m.Status));
        if (Normalize(q.Type) is { } type) query = query.Where(m => m.MissionType.Code == type);
        if (Normalize(q.Department) is { } department) query = query.Where(m => m.ToLocation != null && m.ToLocation.Code == department);
        if (q.RobotId is { } robot) query = query.Where(m => m.Trip != null && m.Trip.RobotId == robot);
        if (Normalize(q.RequestCode) is { } request) query = query.Where(m => m.RequestCode == request);
        if (q.ImportId is { } batch) query = query.Where(m => m.BatchId == batch);
        if (q.From is { } from) query = query.Where(m => m.CreatedAt >= from);
        if (q.To is { } to) query = query.Where(m => m.CreatedAt < to.AddDays(1));
        if (Normalize(q.Q) is { } text) query = query.Where(m => m.MissionCode.Contains(text) || (m.RequestCode != null && m.RequestCode.Contains(text)) || (m.ItemCodeSnapshot != null && m.ItemCodeSnapshot.Contains(text)) || (m.ItemNameSnapshot != null && m.ItemNameSnapshot.Contains(text)));
        return query;
    }

    private static bool IsValid(TaskListQuery q) => Validator.TryValidateObject(q, new ValidationContext(q), null, true) && (!q.From.HasValue || !q.To.HasValue || q.From <= q.To);
    private static TaskQueueItemResponse ToQueueItem(Mission mission, QueueSettings config, DateTime now)
    {
        var priorityPoints = mission.Priority switch { MissionPriority.Normal => config.NormalPriority, MissionPriority.High => config.HighPriority, MissionPriority.Urgent => config.UrgentPriority, _ => 0 };
        var deadlinePoints = 0;
        if (mission.DeadlineAt is { } deadline)
        {
            var minutes = (deadline - now).TotalMinutes;
            deadlinePoints = minutes <= 30 ? 25 : minutes <= 60 ? 15 : minutes <= 120 ? 5 : 0;
        }
        var waitedMinutes = Math.Max(0, (now - mission.CreatedAt).TotalMinutes);
        var waitPoints = Math.Min(config.MaxWaitBonus, (int)Math.Floor(waitedMinutes / config.WaitStepMinutes));
        var redeliveryPoints = mission.Status == MissionStatus.Rescheduled ? config.RedeliveryBonus : 0;
        return new TaskQueueItemResponse { Id = mission.Id, MissionCode = mission.MissionCode, RequestCode = mission.RequestCode, Floor = mission.Zone.Code, MissionType = mission.MissionType.Code,
            Department = mission.ToLocation?.Code, Status = Code(mission.Status), Priority = Code(mission.Priority), CreatedAt = new DateTimeOffset(mission.CreatedAt), DeadlineAt = Offset(mission.DeadlineAt),
            IsOverdue = mission.DeadlineAt is { } value && value < now, PriorityPoints = priorityPoints, DeadlinePoints = deadlinePoints, WaitPoints = waitPoints, RedeliveryPoints = redeliveryPoints,
            PriorityScore = priorityPoints + deadlinePoints + waitPoints + redeliveryPoints };
    }
    private static bool TryReadQueueSettings(IReadOnlyDictionary<string, string> values, out QueueSettings config)
    {
        config = default;
        if (!TryInt(values, QueueSettingKeys.Normal, out var normal) || !TryInt(values, QueueSettingKeys.High, out var high) || !TryInt(values, QueueSettingKeys.Urgent, out var urgent)
            || !TryInt(values, QueueSettingKeys.WaitStep, out var step) || !TryInt(values, QueueSettingKeys.MaxWait, out var max) || !TryInt(values, QueueSettingKeys.Redelivery, out var redelivery)
            || step <= 0 || max < 0 || normal < 0 || high < normal || urgent < high || redelivery < 0) return false;
        config = new QueueSettings(normal, high, urgent, step, max, redelivery); return true;
    }
    private static bool TryInt(IReadOnlyDictionary<string, string> values, string key, out int value)
    {
        value = 0;
        return values.TryGetValue(key, out var text) && int.TryParse(text, out value);
    }
    private static bool TryParseStatuses(string? input, out IReadOnlyList<MissionStatus>? statuses)
    {
        statuses = null; if (string.IsNullOrWhiteSpace(input)) return true;
        var values = new List<MissionStatus>();
        foreach (var text in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (!Enum.TryParse<MissionStatus>(text, true, out var status) || !Enum.IsDefined(status)) return false; else values.Add(status);
        if (values.Count == 0) return false; statuses = values.Distinct().ToList(); return true;
    }
    private static TaskListItemResponse ToListItem(Mission m) => new() { Id = m.Id, MissionCode = m.MissionCode, RequestCode = m.RequestCode, Status = Code(m.Status), Priority = Code(m.Priority), MissionType = m.MissionType.Code, Floor = m.Zone.Code, Department = m.ToLocation?.Code, ItemCode = m.ItemCodeSnapshot, ItemName = m.ItemNameSnapshot, Quantity = m.Quantity, CreatedAt = new DateTimeOffset(m.CreatedAt), DeadlineAt = Offset(m.DeadlineAt), RobotCode = m.Trip?.Robot?.RobotCode, TripCode = m.Trip?.TripCode };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset? Offset(DateTime? value) => value is null ? null : new DateTimeOffset(value.Value);
    private static string Code<T>(T value) where T : struct, Enum => value.ToString().ToUpperInvariant();

    private readonly record struct QueueSettings(int NormalPriority, int HighPriority, int UrgentPriority, int WaitStepMinutes, int MaxWaitBonus, int RedeliveryBonus);
    private static class QueueSettingKeys
    {
        internal const string Normal = "Queue.NormalPriority", High = "Queue.HighPriority", Urgent = "Queue.UrgentPriority", WaitStep = "Queue.WaitStepMinutes", MaxWait = "Queue.MaxWaitBonus", Redelivery = "Queue.RedeliveryBonus";
        internal static readonly string[] All = [Normal, High, Urgent, WaitStep, MaxWait, Redelivery];
    }
}
