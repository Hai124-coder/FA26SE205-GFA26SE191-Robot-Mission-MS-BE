using System.ComponentModel.DataAnnotations;
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

    public async Task<CreateFloorResult> CreateFloorAsync(Guid createdByUserId, CreateFloorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (createdByUserId == Guid.Empty || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)
            || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return CreateFloorResult.Failure(CreateFloorError.InvalidRequest);

        var code = request.Code.Trim();
        if (await repository.FindFloorByCodeAsync(code, cancellationToken) is not null)
            return CreateFloorResult.Failure(CreateFloorError.DuplicateCode);

        var row = await repository.CreateFloorAsync(code, request.Name.Trim(), request.Description?.Trim(), createdByUserId,
            DateTime.UtcNow, cancellationToken);
        return CreateFloorResult.Success(ToFloorResponse(row));
    }

    public async Task<UpdateFloorResult> UpdateFloorAsync(Guid updatedByUserId, string floorCode, UpdateFloorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (updatedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(floorCode)
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)
            || string.IsNullOrWhiteSpace(request.Name))
            return UpdateFloorResult.Failure(UpdateFloorError.InvalidRequest);

        var floor = await repository.FindFloorByCodeAsync(floorCode.Trim(), cancellationToken);
        if (floor is null)
            return UpdateFloorResult.Failure(UpdateFloorError.NotFound);
        if (request.BackgroundImagePath is not null
            && !await repository.HasPublishedMapAsync(floor.Id, cancellationToken))
            return UpdateFloorResult.Failure(UpdateFloorError.MapNotPublished);

        var row = await repository.UpdateFloorAsync(floor.Id, request.Name.Trim(), request.Description?.Trim(),
            request.BackgroundImagePath?.Trim(), updatedByUserId, DateTime.UtcNow, cancellationToken);
        return UpdateFloorResult.Success(ToFloorResponse(row));
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
