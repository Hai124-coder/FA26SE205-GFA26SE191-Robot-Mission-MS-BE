using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Incidents.DTOs;

namespace RobotMissionMS.Business.Features.Incidents.Services;

public interface IIncidentReadService
{
    Task<IncidentListResult> GetIncidentsAsync(IncidentListQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<IncidentDetailResult> GetIncidentAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default);
    Task<IncidentStatsResult> GetStatsAsync(IncidentStatsQuery query, string? operatorFloor, CancellationToken cancellationToken = default);
}

public enum IncidentReadError { InvalidQuery, NotFound, FloorForbidden }

public sealed class IncidentListResult
{
    private IncidentListResult(PagedResponse<IncidentListItemResponse>? response, IncidentReadError? error) { Response = response; Error = error; }
    public PagedResponse<IncidentListItemResponse>? Response { get; }
    public IncidentReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static IncidentListResult Success(PagedResponse<IncidentListItemResponse> response) => new(response, null);
    public static IncidentListResult Failure(IncidentReadError error) => new(null, error);
}

public sealed class IncidentDetailResult
{
    private IncidentDetailResult(IncidentDetailResponse? response, IncidentReadError? error) { Response = response; Error = error; }
    public IncidentDetailResponse? Response { get; }
    public IncidentReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static IncidentDetailResult Success(IncidentDetailResponse response) => new(response, null);
    public static IncidentDetailResult Failure(IncidentReadError error) => new(null, error);
}

public sealed class IncidentStatsResult
{
    private IncidentStatsResult(IncidentStatsResponse? response, IncidentReadError? error) { Response = response; Error = error; }
    public IncidentStatsResponse? Response { get; }
    public IncidentReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static IncidentStatsResult Success(IncidentStatsResponse response) => new(response, null);
    public static IncidentStatsResult Failure(IncidentReadError error) => new(null, error);
}
