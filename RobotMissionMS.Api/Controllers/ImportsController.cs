using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Api.Imports;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
[Authorize(Policy = AuthPolicies.OperatorOnly)]
public sealed class ImportsController(IImportTemplateService templateService) : ControllerBase
{
    [HttpGet("template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var content = await templateService.CreateAsync(cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "RMMS-YC-v3.xlsx");
    }
}
