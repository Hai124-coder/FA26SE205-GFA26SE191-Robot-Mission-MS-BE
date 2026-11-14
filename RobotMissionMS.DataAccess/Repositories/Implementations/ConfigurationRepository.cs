using System.Data;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class ConfigurationRepository(RobotMissionDbContext db) : IConfigurationRepository
{
    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.SystemSettings.AsNoTracking().Where(setting => keys.Contains(setting.Key))
            .Select(setting => new { setting.Key, setting.Value }).ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
    }

    public async Task UpdateValuesAsync(IReadOnlyDictionary<string, string> values, Guid updatedByUserId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var keys = values.Keys.ToList();
        var settings = await db.SystemSettings.Where(setting => keys.Contains(setting.Key)).ToListAsync(cancellationToken);
        if (settings.Count != keys.Count)
            throw new InvalidOperationException("One or more required system settings are missing.");

        var updatedAt = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        foreach (var setting in settings)
        {
            setting.Value = values[setting.Key];
            setting.UpdatedAt = updatedAt;
            setting.UpdatedByUserId = updatedByUserId;
        }

        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = updatedByUserId,
            Timestamp = updatedAt,
            Action = "SYSTEM_CONFIGURATION_UPDATED",
            TargetObject = $"SystemSettings:{string.Join(',', keys.OrderBy(key => key, StringComparer.Ordinal))}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
