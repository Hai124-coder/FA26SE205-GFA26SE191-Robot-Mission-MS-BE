using RobotMissionMS.Business.Features.Locations.DTOs;

namespace RobotMissionMS.Business.Features.Locations.Services;

public enum CreateLocationError { InvalidRequest, DuplicateCode, FloorNotFound, FloorInactive, MapNodeNotFound }
public enum UpdateLocationError { InvalidRequest, NotFound, AmbiguousCode, FloorChangeNotAllowed, MapNodeNotFound }
public enum UpdateLocationStatusError { InvalidRequest, NotFound, AmbiguousCode, InUse }

public sealed record LocationUsage(IReadOnlyList<string> PendingMissionCodes, IReadOnlyList<string> ActiveScheduleNames,
    int ActiveChargingSessions);

public sealed class CreateLocationResult
{
    private CreateLocationResult(LocationResponse? response, CreateLocationError? error) { Response = response; Error = error; }
    public LocationResponse? Response { get; }
    public CreateLocationError? Error { get; }
    public bool IsSuccess => Error is null;
    public static CreateLocationResult Success(LocationResponse response) => new(response, null);
    public static CreateLocationResult Failure(CreateLocationError error) => new(null, error);
}

public sealed class UpdateLocationResult
{
    private UpdateLocationResult(LocationResponse? response, UpdateLocationError? error) { Response = response; Error = error; }
    public LocationResponse? Response { get; }
    public UpdateLocationError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateLocationResult Success(LocationResponse response) => new(response, null);
    public static UpdateLocationResult Failure(UpdateLocationError error) => new(null, error);
}

public sealed class UpdateLocationStatusResult
{
    private UpdateLocationStatusResult(LocationResponse? response, UpdateLocationStatusError? error, LocationUsage? usage)
    { Response = response; Error = error; Usage = usage; }
    public LocationResponse? Response { get; }
    public UpdateLocationStatusError? Error { get; }
    public LocationUsage? Usage { get; }
    public bool IsSuccess => Error is null;
    public static UpdateLocationStatusResult Success(LocationResponse response) => new(response, null, null);
    public static UpdateLocationStatusResult Failure(UpdateLocationStatusError error, LocationUsage? usage = null) => new(null, error, usage);
}
