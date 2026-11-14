using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IMissionTypeRepository
{
    Task<IReadOnlyList<MissionTypeCatalogRow>> GetMissionTypesAsync(CancellationToken cancellationToken = default);
    Task<MissionTypeCatalogRow?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<MissionTypeCatalogRow> UpdateAsync(Guid id, int departDelaySec, int waitTimeoutSec, int basePriority,
        LocationKind? pickupKind, IReadOnlyList<MissionTypePickupLocationTarget>? pickupLocations, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissionTypePickupLocationTarget>?> ResolvePickupLocationsAsync(
        IReadOnlyList<MissionTypePickupLocationLookup> lookups, CancellationToken cancellationToken = default);
    Task<bool> HasActiveWorkAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MissionTypeCatalogRow> UpdateStatusAsync(Guid id, bool isActive, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record MissionTypeCatalogRow(Guid Id, string Code, string Name, string RobotTypeCode, string RobotTypeName,
    MissionMode Mode, LocationKind? PickupKind, bool UsesSlotSensor, int DepartDelaySec, int WaitTimeoutSec, int BasePriority,
    bool IsActive, decimal? MaxLoadPerSlotKg)
{
    public IReadOnlyList<MissionTypePickupLocationRow> PickupLocations { get; init; } = [];
}

public sealed record MissionTypePickupLocationRow(string FloorCode, string LocationCode, string LocationName);
public sealed record MissionTypePickupLocationLookup(string FloorCode, string LocationCode);
public sealed record MissionTypePickupLocationTarget(Guid ZoneId, Guid LocationId);
