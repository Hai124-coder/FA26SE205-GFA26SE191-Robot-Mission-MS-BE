using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class LocationRepository(RobotMissionDbContext db) : ILocationRepository
{
    public async Task<IReadOnlyList<LocationRow>> GetLocationsAsync(string? floorCode, LocationKind? kind,
        CancellationToken cancellationToken = default)
    {
        var query = db.Locations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(floorCode))
            query = query.Where(location => location.Zone.Code == floorCode);
        if (kind.HasValue)
            query = query.Where(location => location.Kind == kind.Value);

        return await query.OrderBy(location => location.Zone.Code).ThenBy(location => location.Code)
            .Select(location => new LocationRow(location.Id, location.Code, location.Name, location.Description,
                location.Zone.Code, location.Zone.Name, location.Kind, location.MapNodeCode, location.IsRestrictedArea,
                location.RestrictedHours, location.IsActive))
            .ToListAsync(cancellationToken);
    }
}
