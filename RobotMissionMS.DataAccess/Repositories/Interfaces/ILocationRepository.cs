using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface ILocationRepository
{
    Task<IReadOnlyList<LocationRow>> GetLocationsAsync(string? floorCode, LocationKind? kind,
        CancellationToken cancellationToken = default);
}

public sealed record LocationRow(Guid Id, string Code, string Name, string? Description, string FloorCode, string FloorName,
    LocationKind Kind, string MapNodeCode, bool IsRestrictedArea, string? RestrictedHours, bool IsActive);
