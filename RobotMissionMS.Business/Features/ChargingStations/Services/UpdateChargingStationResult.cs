using RobotMissionMS.Business.Features.ChargingStations.DTOs;

namespace RobotMissionMS.Business.Features.ChargingStations.Services;

public enum UpdateChargingStationError { InvalidRequest, NotFound, AmbiguousCode, CapacityBelowActiveSessions }

public sealed class UpdateChargingStationResult
{
    private UpdateChargingStationResult(ChargingStationResponse? response, UpdateChargingStationError? error)
    { Response = response; Error = error; }
    public ChargingStationResponse? Response { get; }
    public UpdateChargingStationError? Error { get; }
    public bool IsSuccess => Error is null;
    public static UpdateChargingStationResult Success(ChargingStationResponse response) => new(response, null);
    public static UpdateChargingStationResult Failure(UpdateChargingStationError error) => new(null, error);
}
