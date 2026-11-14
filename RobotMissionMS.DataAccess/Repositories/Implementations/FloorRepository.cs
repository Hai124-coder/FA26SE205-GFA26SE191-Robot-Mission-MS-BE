using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class FloorRepository(RobotMissionDbContext db) : IFloorRepository
{
    public async Task<IReadOnlyList<FloorRow>> GetFloorsAsync(CancellationToken cancellationToken = default) =>
        await db.Zones.AsNoTracking().OrderBy(zone => zone.Code)
            .Select(zone => new FloorRow(zone.Id, zone.Code, zone.Name, zone.Description, zone.IsActive, zone.OperationMode))
            .ToListAsync(cancellationToken);

    public Task<FloorRow?> FindFloorByCodeAsync(string floorCode, CancellationToken cancellationToken = default) =>
        db.Zones.AsNoTracking().Where(zone => zone.Code == floorCode)
            .Select(zone => new FloorRow(zone.Id, zone.Code, zone.Name, zone.Description, zone.IsActive, zone.OperationMode))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PublishedMapRow?> GetPublishedMapAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        var map = await db.MapVersions.AsNoTracking()
            .Where(version => version.ZoneId == zoneId && version.Status == MapVersionStatus.Published)
            .Select(version => new { version.Id, version.VersionNo, version.BackgroundImagePath, version.WidthM, version.HeightM })
            .SingleOrDefaultAsync(cancellationToken);
        if (map is null)
            return null;

        var nodes = await db.MapNodes.AsNoTracking().Where(node => node.MapVersionId == map.Id).OrderBy(node => node.Code)
            .Select(node => new MapNodeRow(node.Id, node.Code, node.NodeType, node.CoordX, node.CoordY))
            .ToListAsync(cancellationToken);
        var edges = await db.MapEdges.AsNoTracking().Where(edge => edge.MapVersionId == map.Id).OrderBy(edge => edge.Id)
            .Select(edge => new MapEdgeRow(edge.Id, edge.FromNodeId, edge.ToNodeId, edge.DistanceMeters,
                edge.IsBidirectional, edge.IsBlocked))
            .ToListAsync(cancellationToken);

        return new PublishedMapRow(map.Id, map.VersionNo, map.BackgroundImagePath, map.WidthM, map.HeightM, nodes, edges);
    }
}
