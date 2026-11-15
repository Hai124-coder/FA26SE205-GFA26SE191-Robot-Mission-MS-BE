using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Tasks.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Tasks.Services;

public sealed class TaskReadService(RobotMissionDbContext db) : ITaskReadService
{
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
}
