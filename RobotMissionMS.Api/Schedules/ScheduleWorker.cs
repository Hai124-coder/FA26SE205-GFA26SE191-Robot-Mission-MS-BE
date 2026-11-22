using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Schedules.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Schedules;

public sealed class ScheduleWorker(IServiceScopeFactory scopes, ILogger<ScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Schedule worker cycle failed."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RobotMissionDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<ScheduleRunProcessor>();
        var now = DateTime.UtcNow;
        var maxWaitMinutes = await GetMaxWaitMinutesAsync(db, ct);
        var local = ToVietnamTime(now);

        var schedules = await db.MissionSchedules
            .Include(x => x.MissionType)
            .Include(x => x.MissionScheduleStops).ThenInclude(x => x.Location)
            .Where(x => x.IsActive && x.DeletedAt == null
                && x.DaysOfWeek.Contains((int)local.DayOfWeek == 0 ? 7 : (int)local.DayOfWeek)
                && x.RunTime <= TimeOnly.FromDateTime(local))
            .ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            var slot = new DateTime(local.Year, local.Month, local.Day, schedule.RunTime.Hour, schedule.RunTime.Minute, 0, DateTimeKind.Unspecified);
            var scheduledFor = TimeZoneInfo.ConvertTimeToUtc(slot, VietnamTimeZone());
            var run = await db.MissionScheduleRuns.SingleOrDefaultAsync(
                x => x.ScheduleId == schedule.Id && !x.IsManual && x.ScheduledFor == scheduledFor, ct);

            if (run is null)
            {
                run = new MissionScheduleRun
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = schedule.Id,
                    ScheduledFor = scheduledFor,
                    Status = ScheduleRunStatus.WaitingForRobot,
                    CreatedAt = now,
                    RouteSnapshot = JsonSerializer.Serialize(new
                    {
                        missionTypeCode = schedule.MissionType.Code,
                        floor = schedule.ZoneId,
                        stops = schedule.MissionScheduleStops.OrderBy(x => x.Seq).Select(x => new { x.Seq, code = x.Location.Code })
                    })
                };
                db.MissionScheduleRuns.Add(run);
                await db.SaveChangesAsync(ct);
            }
        }

        // Includes manual runs created through POST /schedules/{id}/run, which must be dispatched too.
        var waitingRunIds = await db.MissionScheduleRuns
            .Where(x => x.Status == ScheduleRunStatus.WaitingForRobot && x.Schedule.DeletedAt == null)
            .Select(x => x.Id)
            .ToListAsync(ct);

        foreach (var runId in waitingRunIds)
        {
            var result = await processor.EvaluateAsync(runId, maxWaitMinutes, now, ct);
            logger.LogDebug("Schedule run {RunId} evaluated as {Result}", runId, result);
        }
    }

    private static async Task<int> GetMaxWaitMinutesAsync(RobotMissionDbContext db, CancellationToken ct)
    {
        var configured = await db.SystemSettings.Where(x => x.Key == "Schedule.MaxWaitMinutes")
            .Select(x => x.Value).SingleOrDefaultAsync(ct);
        return int.TryParse(configured, out var value) && value > 0 ? value : 15;
    }

    private static DateTime ToVietnamTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone());

    private static TimeZoneInfo VietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }
}
