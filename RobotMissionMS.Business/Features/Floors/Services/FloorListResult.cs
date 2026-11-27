using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Floors.DTOs;

namespace RobotMissionMS.Business.Features.Floors.Services;

public sealed class FloorListResult
{
    private FloorListResult(PagedResponse<FloorResponse>? response) => Response = response;
    public PagedResponse<FloorResponse>? Response { get; }
    public bool IsSuccess => Response is not null;
    public static FloorListResult Success(PagedResponse<FloorResponse> response) => new(response);
    public static FloorListResult Invalid() => new(null);
}
