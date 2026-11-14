using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Entities;
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

    public Task<bool> HasPublishedMapAsync(Guid zoneId, CancellationToken cancellationToken = default) =>
        db.MapVersions.AsNoTracking().AnyAsync(version => version.ZoneId == zoneId
            && version.Status == MapVersionStatus.Published, cancellationToken);

    public async Task<FloorRow> CreateFloorAsync(string code, string name, string? description, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var zone = new Zone { Id = Guid.NewGuid(), Code = code, Name = name, Description = description, IsActive = true,
            OperationMode = ZoneMode.Normal };
        db.Zones.Add(zone);
        AddAuditLog(userId, occurredAtUtc, "FLOOR_CREATED", $"Floor:{code}");
        await db.SaveChangesAsync(cancellationToken);
        return new FloorRow(zone.Id, zone.Code, zone.Name, zone.Description, zone.IsActive, zone.OperationMode);
    }

    public async Task<FloorRow> UpdateFloorAsync(Guid zoneId, string name, string? description, string? backgroundImagePath,
        Guid userId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var zone = await db.Zones.SingleAsync(item => item.Id == zoneId, cancellationToken);
        zone.Name = name;
        zone.Description = description;
        if (backgroundImagePath is not null)
        {
            var map = await db.MapVersions.SingleAsync(version => version.ZoneId == zoneId
                && version.Status == MapVersionStatus.Published, cancellationToken);
            map.BackgroundImagePath = backgroundImagePath;
        }

        AddAuditLog(userId, occurredAtUtc, "FLOOR_UPDATED", $"Floor:{zone.Code}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FloorRow(zone.Id, zone.Code, zone.Name, zone.Description, zone.IsActive, zone.OperationMode);
    }

    private void AddAuditLog(Guid userId, DateTime occurredAtUtc, string action, string targetObject) => db.ActivityLogs.Add(new ActivityLog
    {
        UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), Action = action, TargetObject = targetObject
    });
}
