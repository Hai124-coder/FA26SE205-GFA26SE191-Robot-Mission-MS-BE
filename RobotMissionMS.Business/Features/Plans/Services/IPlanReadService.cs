using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Plans.DTOs;

namespace RobotMissionMS.Business.Features.Plans.Services;

public interface IPlanReadService
{
    Task<PlanReadResult<PlanDetailResponse?>> GetCurrentAsync(PlanCurrentQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<PlanReadResult<PagedResponse<PlanSummaryResponse>>> GetPlansAsync(PlanListQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<PlanReadResult<PlanDetailResponse>> GetPlanAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<PlanReadResult<PagedResponse<TripListItemResponse>>> GetTripsAsync(TripListQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<PlanReadResult<TripDetailResponse>> GetTripAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default);
}

public sealed class PlanReadResult<T>
{
    private PlanReadResult(T? response, PlanReadError? error) { Response = response; Error = error; }
    public T? Response { get; }
    public PlanReadError? Error { get; }
    public bool IsSuccess => Error is null;
    public static PlanReadResult<T> Success(T response) => new(response, null);
    public static PlanReadResult<T> Failure(PlanReadError error) => new(default, error);
}

public enum PlanReadError { InvalidQuery, NotFound, FloorForbidden }
