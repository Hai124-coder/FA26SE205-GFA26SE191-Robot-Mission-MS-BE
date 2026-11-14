using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class MapRepository(RobotMissionDbContext db) : IMapRepository
{
    public Task<MapFloorRow?> FindFloorAsync(string floorCode, CancellationToken cancellationToken = default) => db.Zones.AsNoTracking()
        .Where(zone => zone.Code == floorCode).Select(zone => new MapFloorRow(zone.Id, zone.Code, zone.Name))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MapVersionRow>> GetVersionsAsync(Guid floorId, CancellationToken cancellationToken = default) =>
        await db.MapVersions.AsNoTracking().Where(version => version.ZoneId == floorId).OrderByDescending(version => version.VersionNo)
            .Select(version => new MapVersionRow(version.Id, version.VersionNo, version.Status, version.WidthM, version.HeightM,
                version.BackgroundImagePath, version.ImportedAt, version.PublishedAt, version.MapNodes.Count, version.MapEdges.Count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> FindActiveLocationCodesWithoutNodesAsync(Guid floorId,
        IReadOnlyCollection<string> nodeCodes, CancellationToken cancellationToken = default) =>
        await db.Locations.AsNoTracking().Where(location => location.ZoneId == floorId && location.IsActive
            && !nodeCodes.Contains(location.MapNodeCode)).OrderBy(location => location.Code).Select(location => location.Code)
            .ToListAsync(cancellationToken);

    public async Task<MapVersionRow> CreateDraftAsync(Guid floorId, string sourceJson, string? backgroundImagePath, double widthM,
        double heightM, IReadOnlyList<MapNodeCreateRow> nodes, IReadOnlyList<MapEdgeCreateRow> edges, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Zones.FromSqlInterpolated($"SELECT * FROM public.\"Zones\" WHERE \"Id\" = {floorId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var versionNo = (await db.MapVersions.Where(version => version.ZoneId == floorId).MaxAsync(version => (int?)version.VersionNo,
            cancellationToken) ?? 0) + 1;
        var map = new MapVersion { Id = Guid.NewGuid(), ZoneId = floorId, VersionNo = versionNo, SourceJson = sourceJson,
            BackgroundImagePath = backgroundImagePath, WidthM = widthM, HeightM = heightM, Status = MapVersionStatus.Draft,
            ImportedByUserId = userId, ImportedAt = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc) };
        var nodeEntities = nodes.Select(node => new MapNode { Id = Guid.NewGuid(), MapVersionId = map.Id, Code = node.Code,
            NodeType = node.NodeType, CoordX = node.X, CoordY = node.Y }).ToList();
        var nodeIds = nodeEntities.ToDictionary(node => node.Code, node => node.Id, StringComparer.Ordinal);
        var edgeEntities = edges.Select(edge => new MapEdge { Id = Guid.NewGuid(), MapVersionId = map.Id,
            FromNodeId = nodeIds[edge.FromCode], ToNodeId = nodeIds[edge.ToCode], DistanceMeters = edge.DistanceMeters,
            IsBidirectional = edge.IsBidirectional, IsBlocked = false }).ToList();
        db.MapVersions.Add(map); db.MapNodes.AddRange(nodeEntities); db.MapEdges.AddRange(edgeEntities);
        AddAuditLog(userId, occurredAtUtc, "MAP_VERSION_IMPORTED", $"MapVersion:{map.Id:D};Floor:{floorId:D}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MapVersionRow(map.Id, map.VersionNo, map.Status, map.WidthM, map.HeightM, map.BackgroundImagePath,
            map.ImportedAt, map.PublishedAt, nodeEntities.Count, edgeEntities.Count);
    }

    public async Task<MapPublishData> PublishAsync(Guid floorId, Guid versionId, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Zones.FromSqlInterpolated($"SELECT * FROM public.\"Zones\" WHERE \"Id\" = {floorId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var map = await db.MapVersions.Include(version => version.MapNodes).SingleOrDefaultAsync(version => version.Id == versionId
            && version.ZoneId == floorId, cancellationToken);
        if (map is null) return new MapPublishData(MapPublishDataKind.VersionNotFound, null, Array.Empty<string>());
        if (map.Status != MapVersionStatus.Draft) return new MapPublishData(MapPublishDataKind.VersionNotDraft, null, Array.Empty<string>());
        var affectedLocations = await FindActiveLocationCodesWithoutNodesAsync(floorId,
            map.MapNodes.Select(node => node.Code).ToList(), cancellationToken);
        if (affectedLocations.Count != 0)
            return new MapPublishData(MapPublishDataKind.LocationsUseRemovedNodes, null, affectedLocations);

        var published = await db.MapVersions.Where(version => version.ZoneId == floorId && version.Status == MapVersionStatus.Published)
            .ToListAsync(cancellationToken);
        foreach (var oldVersion in published) oldVersion.Status = MapVersionStatus.Archived;
        map.Status = MapVersionStatus.Published; map.PublishedByUserId = userId;
        map.PublishedAt = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        AddAuditLog(userId, occurredAtUtc, "MAP_VERSION_PUBLISHED", $"MapVersion:{map.Id:D};Floor:{floorId:D}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MapPublishData(MapPublishDataKind.Success, new MapVersionRow(map.Id, map.VersionNo, map.Status, map.WidthM,
            map.HeightM, map.BackgroundImagePath, map.ImportedAt, map.PublishedAt, map.MapNodes.Count,
            await db.MapEdges.CountAsync(edge => edge.MapVersionId == map.Id, cancellationToken)), Array.Empty<string>());
    }

    public async Task<MapEdgeUpdateData> UpdateEdgeAsync(Guid floorId, Guid edgeId, bool isBlocked, string? reason, Guid userId,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var edge = await db.MapEdges.Include(item => item.MapVersion).SingleOrDefaultAsync(item => item.Id == edgeId
            && item.MapVersion.ZoneId == floorId, cancellationToken);
        if (edge is null) return MapEdgeUpdateData.EdgeNotFound;
        if (edge.MapVersion.Status != MapVersionStatus.Published) return MapEdgeUpdateData.EdgeNotPublished;
        edge.IsBlocked = isBlocked; edge.BlockedAt = isBlocked ? DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc) : null;
        edge.BlockedByUserId = isBlocked ? userId : null; edge.BlockReason = isBlocked ? reason : null;
        AddAuditLog(userId, occurredAtUtc, "MAP_EDGE_UPDATED", $"MapEdge:{edge.Id:D};Blocked:{isBlocked}");
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return MapEdgeUpdateData.Success;
    }

    public async Task<FloorMaintenanceData?> UpdateMaintenanceAsync(Guid floorId, bool enabled, Guid userId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var floor = await db.Zones.FromSqlInterpolated($"SELECT * FROM public.\"Zones\" WHERE \"Id\" = {floorId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (floor is null) return null;
        var activeTrips = await db.Trips.CountAsync(trip => trip.ZoneId == floorId
            && (trip.Status == TripStatus.Dispatched || trip.Status == TripStatus.InProgress), cancellationToken);
        floor.OperationMode = enabled ? (activeTrips == 0 ? ZoneMode.Maintenance : ZoneMode.Draining) : ZoneMode.Normal;
        AddAuditLog(userId, occurredAtUtc, "FLOOR_MAINTENANCE_UPDATED", $"Floor:{floor.Code};Mode:{floor.OperationMode}");
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new FloorMaintenanceData(floor.Code, floor.OperationMode, activeTrips);
    }

    private void AddAuditLog(Guid userId, DateTime occurredAtUtc, string action, string targetObject) => db.ActivityLogs.Add(new ActivityLog
    { UserId = userId, Timestamp = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), Action = action, TargetObject = targetObject });
}
