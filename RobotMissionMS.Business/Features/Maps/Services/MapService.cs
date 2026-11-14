using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using RobotMissionMS.Business.Features.Maps.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Maps.Services;

public sealed class MapService(IMapRepository repository) : IMapService
{
    public async Task<MapImportResult> ImportDraftAsync(Guid userId, string floorCode, MapImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(floorCode)) return MapImportResult.Failure(MapImportError.InvalidRequest);
        var floor = await repository.FindFloorAsync(floorCode.Trim(), cancellationToken);
        if (floor is null) return MapImportResult.Failure(MapImportError.FloorNotFound);
        var validation = ValidatePackage(request, out var nodes, out var edges);
        if (validation.Count != 0) return MapImportResult.Failure(MapImportError.InvalidMap, validation);

        var affectedLocationCodes = await repository.FindActiveLocationCodesWithoutNodesAsync(floor.Id,
            nodes.Select(node => node.Code).ToList(), cancellationToken);

        var sourceJson = JsonSerializer.Serialize(new
        {
            widthM = request.WidthM, heightM = request.HeightM, backgroundImagePath = request.BackgroundImagePath?.Trim(),
            nodes = nodes.Select(node => new { code = node.Code, nodeType = ToDatabaseName(node.NodeType), x = node.X, y = node.Y }),
            edges = edges.Select(edge => new { from = edge.FromCode, to = edge.ToCode, edge.DistanceMeters, bidirectional = edge.IsBidirectional })
        });
        var row = await repository.CreateDraftAsync(floor.Id, sourceJson, request.BackgroundImagePath?.Trim(), request.WidthM, request.HeightM,
            nodes, edges, userId, DateTime.UtcNow, cancellationToken);
        var warnings = affectedLocationCodes.Count == 0
            ? Array.Empty<string>()
            : new[] { "The draft omits nodes used by active locations and cannot be published until they are restored or those locations are disabled." };
        return MapImportResult.Success(new MapImportResponse { MapVersion = ToResponse(row), Warnings = warnings,
            AffectedLocationCodes = affectedLocationCodes });
    }

    public async Task<MapVersionListResult> GetVersionsAsync(string floorCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(floorCode)) return MapVersionListResult.Failure(MapVersionListError.FloorNotFound);
        var floor = await repository.FindFloorAsync(floorCode.Trim(), cancellationToken);
        if (floor is null) return MapVersionListResult.Failure(MapVersionListError.FloorNotFound);
        return MapVersionListResult.Success((await repository.GetVersionsAsync(floor.Id, cancellationToken)).Select(ToResponse).ToList());
    }

    public async Task<PublishMapResult> PublishAsync(Guid userId, string floorCode, Guid versionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(floorCode) || versionId == Guid.Empty)
            return PublishMapResult.Failure(PublishMapError.VersionNotFound);
        var floor = await repository.FindFloorAsync(floorCode.Trim(), cancellationToken);
        if (floor is null) return PublishMapResult.Failure(PublishMapError.FloorNotFound);
        var result = await repository.PublishAsync(floor.Id, versionId, userId, DateTime.UtcNow, cancellationToken);
        return result.Kind switch
        {
            MapPublishDataKind.Success => PublishMapResult.Success(ToResponse(result.Version!)),
            MapPublishDataKind.VersionNotFound => PublishMapResult.Failure(PublishMapError.VersionNotFound),
            MapPublishDataKind.VersionNotDraft => PublishMapResult.Failure(PublishMapError.VersionNotDraft),
            MapPublishDataKind.LocationsUseRemovedNodes => PublishMapResult.Failure(PublishMapError.LocationsUseRemovedNodes, result.Locations),
            _ => throw new InvalidOperationException("Map repository returned an invalid publish result.")
        };
    }

    public async Task<UpdateMapEdgeResult> UpdateEdgeAsync(Guid userId, string floorCode, Guid edgeId, UpdateMapEdgeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(floorCode) || edgeId == Guid.Empty
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)
            || (request.IsBlocked && string.IsNullOrWhiteSpace(request.Reason)))
            return UpdateMapEdgeResult.Failure(UpdateMapEdgeError.InvalidRequest);
        var floor = await repository.FindFloorAsync(floorCode.Trim(), cancellationToken);
        if (floor is null) return UpdateMapEdgeResult.Failure(UpdateMapEdgeError.FloorNotFound);
        var result = await repository.UpdateEdgeAsync(floor.Id, edgeId, request.IsBlocked, request.Reason?.Trim(), userId,
            DateTime.UtcNow, cancellationToken);
        return result switch
        {
            MapEdgeUpdateData.Success => UpdateMapEdgeResult.Success(),
            MapEdgeUpdateData.EdgeNotFound => UpdateMapEdgeResult.Failure(UpdateMapEdgeError.EdgeNotFound),
            MapEdgeUpdateData.EdgeNotPublished => UpdateMapEdgeResult.Failure(UpdateMapEdgeError.EdgeNotPublished),
            _ => throw new InvalidOperationException("Map repository returned an invalid edge result.")
        };
    }

    public async Task<FloorMaintenanceResult> UpdateMaintenanceAsync(Guid userId, string floorCode,
        UpdateFloorMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(floorCode)) return FloorMaintenanceResult.Failure(FloorMaintenanceError.FloorNotFound);
        var floor = await repository.FindFloorAsync(floorCode.Trim(), cancellationToken);
        if (floor is null) return FloorMaintenanceResult.Failure(FloorMaintenanceError.FloorNotFound);
        var result = await repository.UpdateMaintenanceAsync(floor.Id, request.Enabled, userId, DateTime.UtcNow, cancellationToken)
            ?? throw new InvalidOperationException("The floor disappeared after its maintenance update.");
        return FloorMaintenanceResult.Success(new FloorMaintenanceResponse { Floor = result.FloorCode,
            OperationMode = result.OperationMode.ToString().ToUpperInvariant(), ActiveTripCount = result.ActiveTripCount });
    }

    private static List<string> ValidatePackage(MapImportRequest request, out IReadOnlyList<MapNodeCreateRow> nodes,
        out IReadOnlyList<MapEdgeCreateRow> edges)
    {
        var errors = new List<string>(); var parsedNodes = new List<MapNodeCreateRow>(); var parsedEdges = new List<MapEdgeCreateRow>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, true)) errors.Add("Map dimensions or fields are invalid.");
        if (request.Nodes.Count == 0) errors.Add("A map must contain at least one node.");
        if (request.Edges.Count == 0) errors.Add("A map must contain at least one edge.");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in request.Nodes)
        {
            if (node is null || !Validator.TryValidateObject(node, new ValidationContext(node!), null, true)) { errors.Add("A node is invalid."); continue; }
            var code = node.Code.Trim();
            if (!codes.Add(code)) { errors.Add($"Duplicate node code: {code}."); continue; }
            if (!TryParseNodeType(node.NodeType, out var nodeType)) { errors.Add($"Invalid node type for {code}."); continue; }
            if (node.X < 0 || node.X > request.WidthM || node.Y < 0 || node.Y > request.HeightM) { errors.Add($"Node {code} is outside map bounds."); continue; }
            parsedNodes.Add(new MapNodeCreateRow(code, nodeType, node.X, node.Y));
        }
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in request.Edges)
        {
            if (edge is null || !Validator.TryValidateObject(edge, new ValidationContext(edge!), null, true)) { errors.Add("An edge is invalid."); continue; }
            var from = edge.From.Trim(); var to = edge.To.Trim();
            if (from == to) { errors.Add($"Edge {from} cannot reference itself."); continue; }
            if (!codes.Contains(from) || !codes.Contains(to)) { errors.Add($"Edge {from}->{to} references an unknown node."); continue; }
            var key = edge.IsBidirectional && string.CompareOrdinal(from, to) > 0 ? $"{to}|{from}|B" : $"{from}|{to}|{(edge.IsBidirectional ? "B" : "D")}";
            if (!edgeKeys.Add(key)) { errors.Add($"Duplicate edge: {from}->{to}."); continue; }
            parsedEdges.Add(new MapEdgeCreateRow(from, to, edge.DistanceMeters, edge.IsBidirectional));
        }
        nodes = parsedNodes; edges = parsedEdges; return errors;
    }

    private static bool TryParseNodeType(string value, out NodeType nodeType)
    {
        nodeType = value.Trim().ToUpperInvariant() switch { "WAYPOINT" => NodeType.Waypoint, "STOP" => NodeType.Stop, "CHARGER" => NodeType.Charger, _ => default };
        return value.Trim().ToUpperInvariant() is "WAYPOINT" or "STOP" or "CHARGER";
    }
    private static string ToDatabaseName(NodeType nodeType) => nodeType switch { NodeType.Waypoint => "WAYPOINT", NodeType.Stop => "STOP", NodeType.Charger => "CHARGER", _ => throw new ArgumentOutOfRangeException(nameof(nodeType)) };
    private static MapVersionResponse ToResponse(MapVersionRow row) => new() { Id = row.Id, VersionNo = row.VersionNo,
        Status = row.Status.ToString().ToUpperInvariant(), WidthM = row.WidthM, HeightM = row.HeightM, BackgroundImagePath = row.BackgroundImagePath,
        ImportedAt = row.ImportedAt, PublishedAt = row.PublishedAt, NodeCount = row.NodeCount, EdgeCount = row.EdgeCount };
}
