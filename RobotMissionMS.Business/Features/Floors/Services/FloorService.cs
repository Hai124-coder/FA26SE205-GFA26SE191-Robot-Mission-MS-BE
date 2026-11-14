using RobotMissionMS.Business.Features.Floors.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Floors.Services;

public sealed class FloorService(IFloorRepository repository) : IFloorService
{
    public async Task<IReadOnlyList<FloorResponse>> GetFloorsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetFloorsAsync(cancellationToken);
        return rows.Select(ToFloorResponse).ToList();
    }

    public async Task<FloorMapResult> GetPublishedMapAsync(string floorCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(floorCode))
            return FloorMapResult.Failure(FloorMapError.FloorNotFound);

        var floor = await repository.FindFloorByCodeAsync(floorCode.Trim(), cancellationToken);
        if (floor is null)
            return FloorMapResult.Failure(FloorMapError.FloorNotFound);

        var map = await repository.GetPublishedMapAsync(floor.Id, cancellationToken);
        if (map is null)
            return FloorMapResult.Failure(FloorMapError.MapNotPublished);

        return FloorMapResult.Success(new FloorMapResponse
        {
            FloorCode = floor.Code,
            FloorName = floor.Name,
            VersionId = map.Id,
            VersionNo = map.VersionNo,
            BackgroundImagePath = map.BackgroundImagePath,
            WidthM = map.WidthM,
            HeightM = map.HeightM,
            Nodes = map.Nodes.Select(node => new MapNodeResponse
            {
                Id = node.Id,
                Code = node.Code,
                NodeType = node.NodeType.ToString().ToUpperInvariant(),
                X = node.X,
                Y = node.Y
            }).ToList(),
            Edges = map.Edges.Select(edge => new MapEdgeResponse
            {
                Id = edge.Id,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                DistanceMeters = edge.DistanceMeters,
                IsBidirectional = edge.IsBidirectional,
                IsBlocked = edge.IsBlocked
            }).ToList()
        });
    }

    private static FloorResponse ToFloorResponse(FloorRow row) => new()
    {
        Id = row.Id,
        Code = row.Code,
        Name = row.Name,
        Description = row.Description,
        IsActive = row.IsActive,
        OperationMode = row.OperationMode.ToString().ToUpperInvariant()
    };
}
