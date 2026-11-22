using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Dashboard.DTOs;
using RobotMissionMS.Business.Features.Dashboard.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = AuthPolicies.SupervisorOnly)]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(DashboardOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardOverviewResponse>> Overview(CancellationToken cancellationToken) =>
        Ok(await dashboard.GetOverviewAsync(cancellationToken));
}
