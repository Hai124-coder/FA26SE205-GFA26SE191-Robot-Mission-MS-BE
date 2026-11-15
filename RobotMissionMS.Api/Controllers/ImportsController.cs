using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Api.Imports;
using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
[Authorize]
public sealed class ImportsController(IImportTemplateService templateService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.OperationsRead)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) && (!Enum.TryParse<BatchStatus>(status, true, out var parsedStatus) || !Enum.IsDefined(parsedStatus)))
            return Problem(type: "IMPORT_STATUS_INVALID", statusCode: 400);
        if (from.HasValue && to.HasValue && from > to)
            return Problem(type: "IMPORT_DATE_RANGE_INVALID", statusCode: 400);
        try { return Ok(await templateService.ListAsync(User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null, status, from, to, cancellationToken)); }
        catch (InvalidOperationException exception) when (exception.Message is "IMPORT_STATUS_INVALID" or "IMPORT_DATE_RANGE_INVALID") { return Problem(type: exception.Message, statusCode: 400); }
    }
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
    [RequestSizeLimit(ImportTemplateService.MaximumUploadBytes)]
    [ProducesResponseType(typeof(ImportBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); try { return Ok(await templateService.ImportAsync(userId, floor, file, cancellationToken)); } catch (InvalidOperationException e) when (e.Message == "FLOORS_INACTIVE") { return Problem(type: e.Message, statusCode: 403); } catch (InvalidOperationException e) when (e.Message == "IMPORT_FILE_TOO_LARGE") { return Problem(type: e.Message, statusCode: 413); } catch (InvalidOperationException e) when (e.Message.StartsWith("IMPORT_")) { return Problem(type: e.Message, statusCode: 400); } }
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperationsRead)]
    [ProducesResponseType(typeof(ImportBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    { var result = await templateService.GetAsync(id, User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null, cancellationToken); return result is null ? NotFound() : Ok(result); }
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); return await templateService.DeleteAsync(id, userId, floor, cancellationToken) switch { ImportDeleteResult.Deleted => NoContent(), ImportDeleteResult.NotFound => NotFound(), ImportDeleteResult.Forbidden => Problem(type: "IMPORT_FORBIDDEN", statusCode: 403), ImportDeleteResult.AlreadyCommitted => Problem(type: "IMPORT_ALREADY_COMMITTED", statusCode: 409), _ => Problem(statusCode: 500) }; }
    [HttpPost("{id:guid}/commit")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    [ProducesResponseType(typeof(ImportCommitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Commit(Guid id, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); try { return Ok(await templateService.CommitAsync(id, userId, floor, cancellationToken)); } catch (InvalidOperationException e) { return e.Message switch { "IMPORT_NOT_FOUND" => NotFound(), "IMPORT_FORBIDDEN" or "FLOORS_INACTIVE" => Problem(type: "IMPORT_FORBIDDEN", statusCode: 403), "IMPORT_ALREADY_COMMITTED" or "IMPORT_STALE" or "REQUEST_CODE_DUPLICATE" => Problem(type: e.Message, statusCode: 409), _ => Problem(type: "IMPORT_INVALID", statusCode: 400) }; } }
}
