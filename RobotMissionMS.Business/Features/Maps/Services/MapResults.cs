using RobotMissionMS.Business.Features.Maps.DTOs;

namespace RobotMissionMS.Business.Features.Maps.Services;

public enum MapImportError { InvalidRequest, FloorNotFound, InvalidMap }
public enum MapVersionListError { FloorNotFound }
public enum PublishMapError { FloorNotFound, VersionNotFound, VersionNotDraft, LocationsUseRemovedNodes }
public enum UpdateMapEdgeError { InvalidRequest, FloorNotFound, EdgeNotFound, EdgeNotPublished }
public enum FloorMaintenanceError { FloorNotFound }

public sealed class MapImportResult
{
    private MapImportResult(MapImportResponse? response, MapImportError? error, IReadOnlyList<string>? errors)
    { Response = response; Error = error; Errors = errors ?? Array.Empty<string>(); }
    public MapImportResponse? Response { get; }
    public MapImportError? Error { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Error is null;
    public static MapImportResult Success(MapImportResponse response) => new(response, null, null);
    public static MapImportResult Failure(MapImportError error, IReadOnlyList<string>? errors = null) => new(null, error, errors);
}

public sealed class MapVersionListResult
{
    private MapVersionListResult(IReadOnlyList<MapVersionResponse>? response, MapVersionListError? error) { Response = response; Error = error; }
    public IReadOnlyList<MapVersionResponse>? Response { get; }
    public MapVersionListError? Error { get; }
    public bool IsSuccess => Error is null;
    public static MapVersionListResult Success(IReadOnlyList<MapVersionResponse> response) => new(response, null);
    public static MapVersionListResult Failure(MapVersionListError error) => new(null, error);
}

public sealed class PublishMapResult
{
    private PublishMapResult(MapVersionResponse? response, PublishMapError? error, IReadOnlyList<string>? locations)
    { Response = response; Error = error; Locations = locations ?? Array.Empty<string>(); }
    public MapVersionResponse? Response { get; }
    public PublishMapError? Error { get; }
    public IReadOnlyList<string> Locations { get; }
    public bool IsSuccess => Error is null;
    public static PublishMapResult Success(MapVersionResponse response) => new(response, null, null);
    public static PublishMapResult Failure(PublishMapError error, IReadOnlyList<string>? locations = null) => new(null, error, locations);
}

public sealed class UpdateMapEdgeResult
{
    private UpdateMapEdgeResult(UpdateMapEdgeError? error) => Error = error;
    public UpdateMapEdgeError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateMapEdgeResult Success() => new(null);
    public static UpdateMapEdgeResult Failure(UpdateMapEdgeError error) => new(error);
}

public sealed class FloorMaintenanceResult
{
    private FloorMaintenanceResult(FloorMaintenanceResponse? response, FloorMaintenanceError? error) { Response = response; Error = error; }
    public FloorMaintenanceResponse? Response { get; }
    public FloorMaintenanceError? Error { get; }
    public bool IsSuccess => Error is null;
    public static FloorMaintenanceResult Success(FloorMaintenanceResponse response) => new(response, null);
    public static FloorMaintenanceResult Failure(FloorMaintenanceError error) => new(null, error);
}
