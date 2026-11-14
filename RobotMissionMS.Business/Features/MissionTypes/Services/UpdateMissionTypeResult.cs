using RobotMissionMS.Business.Features.MissionTypes.DTOs;

namespace RobotMissionMS.Business.Features.MissionTypes.Services;

public enum UpdateMissionTypeError { InvalidRequest, NotFound }

public sealed class UpdateMissionTypeResult
{
    private UpdateMissionTypeResult(MissionTypeResponse? response, UpdateMissionTypeError? error) { Response = response; Error = error; }
    public MissionTypeResponse? Response { get; }
    public UpdateMissionTypeError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateMissionTypeResult Success(MissionTypeResponse response) => new(response, null);
    public static UpdateMissionTypeResult Failure(UpdateMissionTypeError error) => new(null, error);
}
