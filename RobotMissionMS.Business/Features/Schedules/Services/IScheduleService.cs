using RobotMissionMS.Business.Features.Schedules.DTOs;

namespace RobotMissionMS.Business.Features.Schedules.Services;

public interface IScheduleService
{
    Task<IReadOnlyList<ScheduleResponse>> ListAsync(string? floor, CancellationToken ct = default);
    Task<ScheduleResponse?> CreateAsync(Guid user, string floor, ScheduleRequest request, CancellationToken ct = default);
    Task<ScheduleResponse?> UpdateAsync(Guid id, Guid user, string floor, ScheduleRequest request, CancellationToken ct = default);
    Task<bool?> SetStatusAsync(Guid id, Guid user, string floor, bool active, CancellationToken ct = default);
    Task<bool?> DeleteAsync(Guid id, Guid user, string floor, CancellationToken ct = default);
    Task<ScheduleRunResult> RunAsync(Guid id, Guid user, string floor, string key, CancellationToken ct = default);
}
public enum ScheduleRunError { InvalidIdempotencyKey, NotFound, RunInProgress }
public sealed record ScheduleRunResult(ScheduleRunResponse? Response, ScheduleRunError? Error) { public bool IsSuccess=>Error is null; public static ScheduleRunResult Success(ScheduleRunResponse response)=>new(response,null); public static ScheduleRunResult Failure(ScheduleRunError error)=>new(null,error); }
