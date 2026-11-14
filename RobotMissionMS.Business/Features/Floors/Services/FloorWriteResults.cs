using RobotMissionMS.Business.Features.Floors.DTOs;

namespace RobotMissionMS.Business.Features.Floors.Services;

public enum CreateFloorError { InvalidRequest, DuplicateCode }
public enum UpdateFloorError { InvalidRequest, NotFound, MapNotPublished }

public sealed class CreateFloorResult
{
    private CreateFloorResult(FloorResponse? response, CreateFloorError? error) { Response = response; Error = error; }
    public FloorResponse? Response { get; }
    public CreateFloorError? Error { get; }
    public bool IsSuccess => Error is null;
    public static CreateFloorResult Success(FloorResponse response) => new(response, null);
    public static CreateFloorResult Failure(CreateFloorError error) => new(null, error);
}

public sealed class UpdateFloorResult
{
    private UpdateFloorResult(FloorResponse? response, UpdateFloorError? error) { Response = response; Error = error; }
    public FloorResponse? Response { get; }
    public UpdateFloorError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateFloorResult Success(FloorResponse response) => new(response, null);
    public static UpdateFloorResult Failure(UpdateFloorError error) => new(null, error);
}
