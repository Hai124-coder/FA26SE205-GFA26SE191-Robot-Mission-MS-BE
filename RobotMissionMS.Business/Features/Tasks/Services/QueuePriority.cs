using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Tasks.Services;

public readonly record struct QueuePrioritySettings(int NormalPriority, int HighPriority, int UrgentPriority, int WaitStepMinutes, int MaxWaitBonus, int RedeliveryBonus);

public static class QueuePriority
{
    private const string Normal = "Queue.NormalPriority", High = "Queue.HighPriority", Urgent = "Queue.UrgentPriority", WaitStep = "Queue.WaitStepMinutes", MaxWait = "Queue.MaxWaitBonus", Redelivery = "Queue.RedeliveryBonus";
    public static readonly string[] Keys = [Normal, High, Urgent, WaitStep, MaxWait, Redelivery];

    public static async Task<QueuePrioritySettings> LoadAsync(RobotMissionDbContext db, CancellationToken cancellationToken)
    {
        var values = await db.SystemSettings.AsNoTracking().Where(setting => Keys.Contains(setting.Key)).ToDictionaryAsync(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal, cancellationToken);
        if (!TryRead(values, out var settings)) throw new InvalidOperationException("Queue settings are invalid.");
        return settings;
    }

    public static int Score(Mission mission, QueuePrioritySettings settings, DateTime now)
    {
        var priority = mission.Priority switch { MissionPriority.Normal => settings.NormalPriority, MissionPriority.High => settings.HighPriority, MissionPriority.Urgent => settings.UrgentPriority, _ => 0 };
        var deadline = mission.DeadlineAt is { } due ? (due - now).TotalMinutes <= 30 ? 25 : (due - now).TotalMinutes <= 60 ? 15 : (due - now).TotalMinutes <= 120 ? 5 : 0 : 0;
        var waiting = Math.Min(settings.MaxWaitBonus, (int)Math.Floor(Math.Max(0, (now - mission.CreatedAt).TotalMinutes) / settings.WaitStepMinutes));
        return priority + deadline + waiting + (mission.Status == MissionStatus.Rescheduled ? settings.RedeliveryBonus : 0);
    }

    public static bool TryRead(IReadOnlyDictionary<string, string> values, out QueuePrioritySettings settings)
    {
        settings = default;
        if (!Number(Normal, out var normal) || !Number(High, out var high) || !Number(Urgent, out var urgent) || !Number(WaitStep, out var step) || !Number(MaxWait, out var max) || !Number(Redelivery, out var redelivery) || step <= 0 || max < 0 || normal < 0 || high < normal || urgent < high || redelivery < 0) return false;
        settings = new QueuePrioritySettings(normal, high, urgent, step, max, redelivery); return true;
        bool Number(string key, out int value)
        {
            value = 0;
            return values.TryGetValue(key, out var text) && int.TryParse(text, out value);
        }
    }
}
