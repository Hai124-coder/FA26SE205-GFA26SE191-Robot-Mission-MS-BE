using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Schedules.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Schedules.Services;

public sealed class ScheduleService(RobotMissionDbContext db) : IScheduleService
{
    public async Task<IReadOnlyList<ScheduleResponse>> ListAsync(string? floor, CancellationToken ct = default)
    {
        var query = db.MissionSchedules.AsNoTracking().Include(x => x.MissionType).Include(x => x.Zone)
            .Include(x => x.MissionScheduleStops).ThenInclude(x => x.Location).Where(x => x.DeletedAt == null);
        if (floor is not null) query = query.Where(x => x.Zone.Code == floor);
        var schedules = await query.OrderBy(x => x.Name).ToListAsync(ct);
        var ids = schedules.Select(x => x.Id).ToArray();
        var runs = ids.Length == 0 ? [] : await db.MissionScheduleRuns.AsNoTracking().Where(x => ids.Contains(x.ScheduleId)).ToListAsync(ct);
        var latest = runs.GroupBy(x => x.ScheduleId).ToDictionary(x => x.Key, x => x.OrderByDescending(run => run.ScheduledFor).ThenByDescending(run => run.CreatedAt).First());
        var now = DateTime.UtcNow;
        return schedules.Select(x => Map(x, latest.GetValueOrDefault(x.Id), now)).ToList();
    }

    public async Task<ScheduleResponse?> CreateAsync(Guid user, string floor, ScheduleRequest request, CancellationToken ct = default)
    {
        if (!Valid(request, out var time)) return null;
        var zone = await db.Zones.SingleOrDefaultAsync(x => x.Code == floor && x.IsActive, ct);
        var type = await db.MissionTypes.SingleOrDefaultAsync(x => x.Code == request.MissionTypeCode && x.IsActive && x.Mode == MissionMode.Schedule, ct);
        if (zone is null || type is null || request.MissionTypeCode is not ("WASTE_COLLECTION" or "CORRIDOR_CLEANING")) return null;
        var locations = await LocationsAsync(request.Stops, zone.Id, ct);
        if (locations is null) return null;

        var now = DateTime.UtcNow;
        var schedule = new MissionSchedule { Id = Guid.NewGuid(), Name = request.Name.Trim(), MissionTypeId = type.Id, ZoneId = zone.Id, DaysOfWeek = request.DaysOfWeek, RunTime = time, IsActive = request.IsActive, CreatedByUserId = user, CreatedAt = now };
        AddStops(schedule, locations, request.Stops);
        db.MissionSchedules.Add(schedule);
        AddActivity(user, now, "SCHEDULE_CREATED", ScheduleTarget(schedule.Id));
        await db.SaveChangesAsync(ct);
        return Map(schedule, null, now, type.Code, zone.Code, locations);
    }

    public async Task<ScheduleResponse?> UpdateAsync(Guid id, Guid user, string floor, ScheduleRequest request, CancellationToken ct = default)
    {
        if (!Valid(request, out var time)) return null;
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        var schedule = await db.MissionSchedules.Include(x => x.Zone).Include(x => x.MissionType).Include(x => x.MissionScheduleStops).ThenInclude(x => x.Location)
            .SingleOrDefaultAsync(x => x.Id == id && x.Zone.Code == floor && x.DeletedAt == null, ct);
        if (schedule is null) return null;
        var type = await db.MissionTypes.SingleOrDefaultAsync(x => x.Code == request.MissionTypeCode && x.IsActive && x.Mode == MissionMode.Schedule, ct);
        var locations = await LocationsAsync(request.Stops, schedule.ZoneId, ct);
        if (type is null || locations is null || request.MissionTypeCode is not ("WASTE_COLLECTION" or "CORRIDOR_CLEANING")) return null;

        var now = DateTime.UtcNow;
        schedule.Name = request.Name.Trim(); schedule.MissionTypeId = type.Id; schedule.RunTime = time; schedule.DaysOfWeek = request.DaysOfWeek; schedule.IsActive = request.IsActive;
        db.MissionScheduleStops.RemoveRange(schedule.MissionScheduleStops);
        AddStops(schedule, locations, request.Stops);
        AddActivity(user, now, "SCHEDULE_UPDATED", ScheduleTarget(schedule.Id));
        await db.SaveChangesAsync(ct);
        if (ownsTransaction) await transaction!.CommitAsync(ct);
        return Map(schedule, null, now, type.Code, schedule.Zone.Code, locations);
    }

    public async Task<bool?> SetStatusAsync(Guid id, Guid user, string floor, bool active, CancellationToken ct = default)
    {
        var schedule = await db.MissionSchedules.SingleOrDefaultAsync(x => x.Id == id && x.Zone.Code == floor && x.DeletedAt == null, ct);
        if (schedule is null) return null;
        if (schedule.IsActive == active) return true;
        schedule.IsActive = active;
        AddActivity(user, DateTime.UtcNow, "SCHEDULE_STATUS_CHANGED", ScheduleTarget(schedule.Id));
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool?> DeleteAsync(Guid id, Guid user, string floor, CancellationToken ct = default)
    {
        var schedule = await db.MissionSchedules.SingleOrDefaultAsync(x => x.Id == id && x.Zone.Code == floor && x.DeletedAt == null, ct);
        if (schedule is null) return null;
        if (await db.MissionScheduleRuns.AnyAsync(x => x.ScheduleId == id && (x.Status == ScheduleRunStatus.WaitingForRobot || x.Status == ScheduleRunStatus.Late), ct)) return false;
        var now = DateTime.UtcNow;
        schedule.DeletedAt = now;
        AddActivity(user, now, "SCHEDULE_DELETED", ScheduleTarget(schedule.Id));
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ScheduleRunResult> RunAsync(Guid id, Guid user, string floor, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return ScheduleRunResult.Failure(ScheduleRunError.InvalidIdempotencyKey);
        var schedule = await db.MissionSchedules.Include(x => x.Zone).Include(x => x.MissionType).Include(x => x.MissionScheduleStops).ThenInclude(x => x.Location)
            .SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null && x.Zone.Code == floor, ct);
        if (schedule is null) return ScheduleRunResult.Failure(ScheduleRunError.NotFound);
        var old = await db.MissionScheduleRuns.SingleOrDefaultAsync(x => x.ScheduleId == id && x.IsManual && x.IdempotencyKey == key, ct);
        if (old is not null) return ScheduleRunResult.Success(new ScheduleRunResponse { RunId = old.Id, Status = StatusCode(old.Status), Replayed = true });
        if (await db.MissionScheduleRuns.AnyAsync(x => x.ScheduleId == id && (x.Status == ScheduleRunStatus.WaitingForRobot || x.Status == ScheduleRunStatus.Late), ct)) return ScheduleRunResult.Failure(ScheduleRunError.RunInProgress);

        var snapshot = System.Text.Json.JsonSerializer.Serialize(new { missionTypeCode = schedule.MissionType.Code, floor = schedule.Zone.Code, stops = schedule.MissionScheduleStops.OrderBy(x => x.Seq).Select(x => new { x.Seq, x.Location.Code }) });
        var now = DateTime.UtcNow;
        var run = new MissionScheduleRun { Id = Guid.NewGuid(), ScheduleId = id, ScheduledFor = now, IsManual = true, IdempotencyKey = key, RequestedByUserId = user, Status = ScheduleRunStatus.WaitingForRobot, RouteSnapshot = snapshot, CreatedAt = now };
        db.MissionScheduleRuns.Add(run);
        AddActivity(user, now, "SCHEDULE_RUN_REQUESTED", RunTarget(run.Id));
        await db.SaveChangesAsync(ct);
        return ScheduleRunResult.Success(new ScheduleRunResponse { RunId = run.Id, Status = StatusCode(run.Status), Replayed = false });
    }

    private static bool Valid(ScheduleRequest request, out TimeOnly time)
    {
        time = default;
        return request.DaysOfWeek.Distinct().Count() == request.DaysOfWeek.Count
            && !request.DaysOfWeek.Any(x => x < 1 || x > 7)
            && TimeOnly.TryParseExact(request.RunTime, "HH:mm", out time);
    }
    private async Task<List<Location>?> LocationsAsync(IReadOnlyList<string> codes, Guid zoneId, CancellationToken ct)
    {
        var locations = await db.Locations.Where(x => codes.Contains(x.Code) && x.ZoneId == zoneId && x.IsActive).ToListAsync(ct);
        return locations.Count == codes.Count && locations.Select(x => x.Code).Distinct().Count() == codes.Count ? locations : null;
    }
    private static void AddStops(MissionSchedule schedule, IReadOnlyList<Location> locations, IReadOnlyList<string> codes)
    {
        for (var index = 0; index < codes.Count; index++) schedule.MissionScheduleStops.Add(new MissionScheduleStop { Id = Guid.NewGuid(), ScheduleId = schedule.Id, Seq = index + 1, LocationId = locations.Single(x => x.Code == codes[index]).Id });
    }
    private static ScheduleResponse Map(MissionSchedule schedule, MissionScheduleRun? latest, DateTime now, string? typeCode = null, string? floor = null, IReadOnlyList<Location>? locations = null) => new()
    {
        Id = schedule.Id, Name = schedule.Name, MissionTypeCode = typeCode ?? schedule.MissionType.Code, Floor = floor ?? schedule.Zone.Code, DaysOfWeek = schedule.DaysOfWeek,
        RunTime = schedule.RunTime.ToString("HH:mm"), Stops = (locations ?? schedule.MissionScheduleStops.Select(x => x.Location).ToList()).Select(x => x.Code).ToList(), IsActive = schedule.IsActive,
        LastRunAt = latest?.ScheduledFor, LastResult = latest is null ? null : StatusCode(latest.Status), NextRunAt = NextRunAt(schedule, now)
    };
    private static DateTime? NextRunAt(MissionSchedule schedule, DateTime now)
    {
        if (!schedule.IsActive) return null;
        var zone = VietnamTimeZone(); var local = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
        for (var offset = 0; offset <= 7; offset++)
        {
            var date = local.Date.AddDays(offset); var day = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;
            var candidate = new DateTime(date.Year, date.Month, date.Day, schedule.RunTime.Hour, schedule.RunTime.Minute, 0, DateTimeKind.Unspecified);
            if (schedule.DaysOfWeek.Contains(day) && TimeZoneInfo.ConvertTimeToUtc(candidate, zone) > now) return TimeZoneInfo.ConvertTimeToUtc(candidate, zone);
        }
        return null;
    }
    private static TimeZoneInfo VietnamTimeZone() { try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); } }
    private static string StatusCode(ScheduleRunStatus status) => status switch { ScheduleRunStatus.WaitingForRobot => "WAITING_FOR_ROBOT", ScheduleRunStatus.MissionCreated => "MISSION_CREATED", _ => status.ToString().ToUpperInvariant() };
    private void AddActivity(Guid? user, DateTime timestamp, string action, string target) => db.ActivityLogs.Add(new ActivityLog { UserId = user, Timestamp = timestamp, Action = action, TargetObject = target });
    private static string ScheduleTarget(Guid id) => $"Schedule:{id:D}";
    private static string RunTarget(Guid id) => $"ScheduleRun:{id:D}";
}
