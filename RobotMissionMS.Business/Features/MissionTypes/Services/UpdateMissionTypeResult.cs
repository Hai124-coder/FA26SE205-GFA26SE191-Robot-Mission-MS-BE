using RobotMissionMS.Business.Features.MissionTypes.DTOs;

namespace RobotMissionMS.Business.Features.MissionTypes.Services;

public enum UpdateMissionTypeError { InvalidRequest, NotFound, PickupLocationNotFound }
public enum UpdateMissionTypeStatusError { InvalidRequest, NotFound, ActiveWorkExists }

public sealed class UpdateMissionTypeResult
{
    private UpdateMissionTypeResult(MissionTypeResponse? response, UpdateMissionTypeError? error) { Response = response; Error = error; }
    public MissionTypeResponse? Response { get; }
    public UpdateMissionTypeError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateMissionTypeResult Success(MissionTypeResponse response) => new(response, null);
    public static UpdateMissionTypeResult Failure(UpdateMissionTypeError error) => new(null, error);
}

public sealed class UpdateMissionTypeStatusResult
{
    private UpdateMissionTypeStatusResult(MissionTypeResponse? response, UpdateMissionTypeStatusError? error)
    { Response = response; Error = error; }
    public MissionTypeResponse? Response { get; } public UpdateMissionTypeStatusError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateMissionTypeStatusResult Success(MissionTypeResponse response) => new(response, null);
    public static UpdateMissionTypeStatusResult Failure(UpdateMissionTypeStatusError error) => new(null, error);
}
