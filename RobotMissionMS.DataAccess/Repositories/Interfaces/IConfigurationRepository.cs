namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IConfigurationRepository
{
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default);
    Task UpdateValuesAsync(IReadOnlyDictionary<string, string> values, Guid updatedByUserId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}
