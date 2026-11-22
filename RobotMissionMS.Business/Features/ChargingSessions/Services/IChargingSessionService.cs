using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.ChargingSessions.DTOs;

namespace RobotMissionMS.Business.Features.ChargingSessions.Services;

public interface IChargingSessionService
{
    Task<ChargingSessionListResult> ListAsync(ChargingSessionListQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<ChargingSessionCommandResult> QueueAsync(Guid robotId, Guid stationId, DateTime now, CancellationToken cancellationToken = default);
    Task<ChargingSessionCommandResult> StartAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken = default);
    Task<ChargingSessionCommandResult> CompleteAsync(Guid sessionId, int batteryAtEnd, DateTime now, CancellationToken cancellationToken = default);
    Task<ChargingSessionCommandResult> InterruptAsync(Guid sessionId, int batteryAtEnd, DateTime now, CancellationToken cancellationToken = default);
}

public enum ChargingSessionReadError { InvalidQuery }
public sealed record ChargingSessionListResult(PagedResponse<ChargingSessionResponse>? Response, ChargingSessionReadError? Error)
{
    public bool IsSuccess => Error is null;
    public static ChargingSessionListResult Success(PagedResponse<ChargingSessionResponse> response) => new(response, null);
    public static ChargingSessionListResult Failure(ChargingSessionReadError error) => new(null, error);
}

public enum ChargingSessionCommandError { RobotNotFound, StationNotFound, FloorMismatch, RobotNotAvailable, ActiveSessionExists, SessionNotFound, SessionNotQueued, SessionNotCharging, StationUnavailable, StationAtCapacity, InvalidBattery }
public sealed record ChargingSessionCommandResult(ChargingSessionResponse? Response, ChargingSessionCommandError? Error)
{
    public bool IsSuccess => Error is null;
    public static ChargingSessionCommandResult Success(ChargingSessionResponse response) => new(response, null);
    public static ChargingSessionCommandResult Failure(ChargingSessionCommandError error) => new(null, error);
}
