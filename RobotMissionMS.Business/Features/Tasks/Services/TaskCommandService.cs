using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.ActivityLogs.Services;
using RobotMissionMS.Business.Features.Tasks.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Tasks.Services;

public sealed class TaskCommandService(RobotMissionDbContext db, TimeProvider? timeProvider = null, IMissionRealtimePublisher? realtime = null, IActivityRealtimePublisher? activityRealtime = null) : ITaskCommandService
{
    private const string CargoReturnedAction = "MISSION_CARGO_RETURNED";
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<TaskCommandResult> UpdateAsync(Guid id, Guid userId, string floor, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        if (!Valid(request) || id == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(floor) || !HasUpdate(request)
            || !TryPriority(request.Priority, out var priority) || !TryDeadline(request.Deadline, out var deadline)) return TaskCommandResult.Failure(TaskCommandError.InvalidRequest);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await LockMissionAsync(id, cancellationToken);
        var access = CheckEditable(mission, floor);
        if (access is { } error) return TaskCommandResult.Failure(error);
        var editableMission = mission!;
        if (request.Quantity is { } quantity)
        {
            var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(value => value.Id == editableMission.ItemId && value.IsActive, cancellationToken);
            if (item is null || item.MaxQtyPerSlot < quantity || editableMission.KgPerUnitSnapshot is not > 0) return TaskCommandResult.Failure(TaskCommandError.ItemConfigurationInvalid);
            editableMission.Quantity = quantity;
            editableMission.WeightKgSnapshot = quantity * editableMission.KgPerUnitSnapshot.Value;
        }
        if (request.Priority is not null) editableMission.Priority = priority;
        if (request.Deadline is not null) editableMission.DeadlineAt = deadline;
        if (request.Note is not null) editableMission.Note = request.Note.Trim();
        var now = clock.GetUtcNow().UtcDateTime;
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = now, Action = "TASK_UPDATED", TargetObject = Target(id) });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        if (realtime is not null) await realtime.PublishTaskUpdatedAsync(editableMission.Zone.Code, Update(editableMission), cancellationToken);
        if (activityRealtime is not null) await activityRealtime.PublishAsync(editableMission.Zone.Code, new ActivityRealtimeEvent(now, "TASK_UPDATED", Target(id), userId), cancellationToken);
        return TaskCommandResult.Success(new TaskActionResponse { TaskId = id, Status = Code(editableMission.Status) });
    }

    public async Task<TaskCommandResult> CancelAsync(Guid id, Guid userId, string floor, CancelTaskRequest request, CancellationToken cancellationToken = default)
    {
        if (!Valid(request) || id == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(floor)) return TaskCommandResult.Failure(TaskCommandError.InvalidRequest);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await LockMissionAsync(id, cancellationToken);
        if (mission is null) return TaskCommandResult.Failure(TaskCommandError.NotFound);
        if (mission.Zone.Code != floor) return TaskCommandResult.Failure(TaskCommandError.FloorForbidden);
        if (mission.Status == MissionStatus.DeliveryFailed && !await HasCargoReturnedAsync(id, cancellationToken)) return TaskCommandResult.Failure(TaskCommandError.CargoNotReturned);
        if (mission.Status is not MissionStatus.Pending and not MissionStatus.Rescheduled and not MissionStatus.DeliveryFailed) return TaskCommandResult.Failure(TaskCommandError.NotPending);
        await TransitionAsync(mission, MissionStatus.Cancelled, userId, request.Reason!.Trim(), "TASK_CANCELLED", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishTransitionAsync(mission, request.Reason!.Trim(), "TASK_CANCELLED", userId, cancellationToken);
        return TaskCommandResult.Success(new TaskActionResponse { TaskId = id, Status = "CANCELLED" });
    }

    public async Task<TaskCommandResult> RedeliverAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(floor)) return TaskCommandResult.Failure(TaskCommandError.InvalidRequest);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await LockMissionAsync(id, cancellationToken);
        if (mission is null) return TaskCommandResult.Failure(TaskCommandError.NotFound);
        if (mission.Zone.Code != floor) return TaskCommandResult.Failure(TaskCommandError.FloorForbidden);
        if (mission.Status != MissionStatus.DeliveryFailed) return TaskCommandResult.Failure(TaskCommandError.NotPending);
        if (!await HasCargoReturnedAsync(id, cancellationToken)) return TaskCommandResult.Failure(TaskCommandError.CargoNotReturned);
        var failures = await db.MissionStatusHistories.CountAsync(history => history.MissionId == id && history.ToStatus == MissionStatus.DeliveryFailed, cancellationToken);
        await TransitionAsync(mission, MissionStatus.Rescheduled, userId, "Redelivery requested after cargo return.", "TASK_REDELIVERED", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishTransitionAsync(mission, "Redelivery requested after cargo return.", "TASK_REDELIVERED", userId, cancellationToken);
        return TaskCommandResult.Success(new TaskActionResponse { TaskId = id, Status = "RESCHEDULED", Warnings = failures >= 2 ? ["TASK_REDELIVERY_REPEATED_FAILURE"] : [] });
    }

    private async Task<Mission?> LockMissionAsync(Guid id, CancellationToken ct) => await db.Missions
        .FromSqlInterpolated($"SELECT * FROM public.\"Missions\" WHERE \"Id\" = {id} FOR UPDATE")
        .Include(mission => mission.Zone).SingleOrDefaultAsync(ct);
    private async Task<bool> HasCargoReturnedAsync(Guid id, CancellationToken ct) => await db.ActivityLogs.AnyAsync(log => log.Action == CargoReturnedAction && log.TargetObject == Target(id), ct);
    private async Task TransitionAsync(Mission mission, MissionStatus to, Guid userId, string reason, string action, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var from = mission.Status; mission.Status = to;
        db.MissionStatusHistories.Add(new MissionStatusHistory { Id = Guid.NewGuid(), MissionId = mission.Id, ChangedAt = now, ChangedByUserId = userId, FromStatus = from, ToStatus = to, Reason = reason });
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Timestamp = now, Action = action, TargetObject = Target(mission.Id) });
        await db.SaveChangesAsync(ct);
    }
    private static TaskCommandError? CheckEditable(Mission? mission, string floor) => mission is null ? TaskCommandError.NotFound : mission.Zone.Code != floor ? TaskCommandError.FloorForbidden : mission.Status is MissionStatus.Pending or MissionStatus.Rescheduled ? null : TaskCommandError.NotPending;
    private static bool Valid(object request) => Validator.TryValidateObject(request, new ValidationContext(request), null, true);
    private static bool HasUpdate(UpdateTaskRequest request) => request.Quantity is not null || request.Priority is not null || request.Deadline is not null || request.Note is not null;
    private static bool TryPriority(string? text, out MissionPriority priority) { priority = MissionPriority.Normal; return text is null || (Enum.TryParse<MissionPriority>(text, true, out priority) && Enum.IsDefined(priority)); }
    private static bool TryDeadline(string? text, out DateTime? deadline) { deadline = null; if (text is null) return true; if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false; var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh"); deadline = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified), zone); return true; }
    private static string Target(Guid id) => $"Mission:{id:D}";
    private async Task PublishTransitionAsync(Mission mission, string reason, string action, Guid actorUserId, CancellationToken ct)
    {
        if (realtime is not null)
        {
            await realtime.PublishTaskUpdatedAsync(mission.Zone.Code, Update(mission, reason), ct);
            var waiting = await db.Missions.CountAsync(value => value.ZoneId == mission.ZoneId && (value.Status == MissionStatus.Pending || value.Status == MissionStatus.Rescheduled), ct);
            await realtime.PublishQueueChangedAsync(mission.Zone.Code, waiting, ct);
        }
        if (activityRealtime is not null) await activityRealtime.PublishAsync(mission.Zone.Code, new ActivityRealtimeEvent(clock.GetUtcNow().UtcDateTime, action, Target(mission.Id), actorUserId), ct);
    }
    private static MissionRealtimeUpdate Update(Mission mission, string? reason = null) => new(mission.Id, Code(mission.Status), mission.Trip?.RobotId, mission.SlotNo, reason);
    private static string Code<T>(T value) where T : struct, Enum => value.ToString().ToUpperInvariant();
}
