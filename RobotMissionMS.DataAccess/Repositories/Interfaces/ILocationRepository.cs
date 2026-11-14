using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface ILocationRepository
{
    Task<IReadOnlyList<LocationRow>> GetLocationsAsync(string? floorCode, LocationKind? kind,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationManagementRow>> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<FloorLookupRow?> FindFloorAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> PublishedMapContainsNodeAsync(Guid floorId, string nodeCode, CancellationToken cancellationToken = default);
    Task<LocationManagementRow> CreateLocationAsync(string code, string name, string? description, Guid floorId, string floorCode,
        string floorName, LocationKind kind, string mapNodeCode, bool isRestrictedArea, string? restrictedHours, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task<LocationManagementRow> UpdateLocationAsync(Guid locationId, string name, string? description, LocationKind kind,
        string mapNodeCode, bool isRestrictedArea, string? restrictedHours, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
    Task<LocationManagementRow> UpdateLocationStatusAsync(Guid locationId, bool isActive, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
    Task<LocationUsageRow> GetUsageAsync(Guid locationId, CancellationToken cancellationToken = default);
}

public sealed record LocationRow(Guid Id, string Code, string Name, string? Description, string FloorCode, string FloorName,
    LocationKind Kind, string MapNodeCode, bool IsRestrictedArea, string? RestrictedHours, bool IsActive);
public sealed record FloorLookupRow(Guid Id, string Code, string Name, bool IsActive);
public sealed record LocationManagementRow(Guid Id, string Code, string Name, string? Description, Guid FloorId, string FloorCode,
    string FloorName, LocationKind Kind, string MapNodeCode, bool IsRestrictedArea, string? RestrictedHours, bool IsActive);
public sealed record LocationUsageRow(IReadOnlyList<string> PendingMissionCodes, IReadOnlyList<string> ActiveScheduleNames,
    int ActiveChargingSessions);
