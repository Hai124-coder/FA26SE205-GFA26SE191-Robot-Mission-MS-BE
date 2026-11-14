using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IMissionTypeRepository
{
    Task<IReadOnlyList<MissionTypeCatalogRow>> GetMissionTypesAsync(CancellationToken cancellationToken = default);
    Task<MissionTypeCatalogRow?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<MissionTypeCatalogRow> UpdateAsync(Guid id, int departDelaySec, int waitTimeoutSec, int basePriority,
        LocationKind? pickupKind, Guid userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
}

public sealed record MissionTypeCatalogRow(Guid Id, string Code, string Name, string RobotTypeCode, string RobotTypeName,
    MissionMode Mode, LocationKind? PickupKind, bool UsesSlotSensor, int DepartDelaySec, int WaitTimeoutSec, int BasePriority);
