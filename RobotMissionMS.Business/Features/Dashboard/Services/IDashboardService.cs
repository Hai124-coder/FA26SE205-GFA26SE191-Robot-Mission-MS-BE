using RobotMissionMS.Business.Features.Dashboard.DTOs;

namespace RobotMissionMS.Business.Features.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
}
