using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Api.Imports;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
[Authorize]
public sealed class ImportsController(IImportTemplateService templateService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.OperationsRead)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        => Ok(await templateService.ListAsync(User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null, status, from, to, cancellationToken));
    [HttpGet("template")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var content = await templateService.CreateAsync(cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "RMMS-YC-v3.xlsx");
    }
    [HttpPost]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); try { return Ok(await templateService.ImportAsync(userId, floor, file, cancellationToken)); } catch (InvalidOperationException e) when (e.Message.StartsWith("IMPORT_")) { return Problem(type: e.Message, statusCode: 400); } }
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperationsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    { var result = await templateService.GetAsync(id, User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null, cancellationToken); return result is null ? NotFound() : Ok(result); }
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); return await templateService.DeleteAsync(id, userId, floor, cancellationToken) ? NoContent() : NotFound(); }
    [HttpPost("{id:guid}/commit")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Commit(Guid id, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); try { return Ok(await templateService.CommitAsync(id, userId, floor, cancellationToken)); } catch (InvalidOperationException e) { return e.Message switch { "IMPORT_NOT_FOUND" => NotFound(), "IMPORT_FORBIDDEN" => Problem(type: "FLOOR_FORBIDDEN", statusCode: 403), _ => Problem(type: e.Message, statusCode: 409) }; } }
}
