using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Api.Imports;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/hospital-files")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class HospitalFilesController(IImportTemplateService imports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HospitalFileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool? imported, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        => Ok(await imports.ListHospitalFilesAsync(imported, from, to, cancellationToken));
    [HttpPost("{id:guid}/import")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    [ProducesResponseType(typeof(ImportBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Import(Guid id, CancellationToken cancellationToken)
    { if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) || User.FindFirstValue("rmms:floor") is not { } floor) return Unauthorized(); try { return Ok(await imports.ImportHospitalFileAsync(id, userId, floor, cancellationToken)); } catch (InvalidOperationException e) { return e.Message switch { "HOSPITAL_FILE_NOT_FOUND" => NotFound(), "HOSPITAL_FILE_ALREADY_IMPORTED" => Problem(type: e.Message, statusCode: 409), _ => Problem(type: e.Message, statusCode: 400) }; } catch (DbUpdateException) { return Problem(type: "HOSPITAL_FILE_ALREADY_IMPORTED", statusCode: 409); } }
}
