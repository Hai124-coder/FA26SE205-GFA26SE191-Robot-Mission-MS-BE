using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Api.Contracts;
using RobotMissionMS.Business.Features.Maps.DTOs;
using RobotMissionMS.Business.Features.Maps.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/floors")]
[Authorize]
public sealed class MapsController(IMapService mapService) : ControllerBase
{
    private const long MaxMapPackageSize = 2 * 1024 * 1024;

    [HttpGet("{floorCode}/map-versions")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<MapVersionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MapVersionResponse>>> GetVersions(
        string floorCode,
        CancellationToken cancellationToken)
    {
        var result = await mapService.GetVersionsAsync(floorCode, cancellationToken);
        return result.Error is MapVersionListError.FloorNotFound
            ? NotFound(new ProblemDetails { Title = "Floor not found", Detail = $"Floor '{floorCode}' was not found." })
            : Ok(result.Response);
    }

    [HttpPost("{floorCode}/map-versions")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MapImportResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<MapImportResponse>> ImportDraft(
        string floorCode,
        [FromForm] MapPackageUploadForm upload,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var file = upload.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Map package is required", Detail = "Provide a non-empty JSON file in the 'file' field." });
        }

        if (file.Length > MaxMapPackageSize)
        {
            return BadRequest(new ProblemDetails { Title = "Map package is too large", Detail = "The map package must not exceed 2 MB." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails { Title = "Invalid map package", Detail = "The map package must be a JSON file." });
        }

        MapImportRequest? request;
        try
        {
            await using var stream = file.OpenReadStream();
            request = await JsonSerializer.DeserializeAsync<MapImportRequest>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid map package", Detail = "The uploaded file is not valid JSON." });
        }

        if (request is null)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid map package", Detail = "The uploaded file must contain a map package." });
        }

        var result = await mapService.ImportDraftAsync(userId, floorCode, request, cancellationToken);
        if (result.Error is MapImportError.FloorNotFound)
        {
            return NotFound(new ProblemDetails { Title = "Floor not found", Detail = $"Floor '{floorCode}' was not found." });
        }

        if (result.Errors.Count > 0)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid map package",
                Detail = "Fix the reported map package errors and upload it again."
            };
            problem.Extensions["errors"] = result.Errors;
            return BadRequest(problem);
        }

        return CreatedAtAction(nameof(GetVersions), new { floorCode }, result.Response);
    }

    [HttpPost("{floorCode}/map-versions/{versionId:guid}/publish")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(MapVersionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MapVersionResponse>> Publish(
        string floorCode,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mapService.PublishAsync(userId, floorCode, versionId, cancellationToken);
        if (result.Error is PublishMapError.FloorNotFound or PublishMapError.VersionNotFound)
        {
            return NotFound(new ProblemDetails { Title = "Map version not found", Detail = "The requested floor or map version was not found." });
        }

        if (result.Error is PublishMapError.LocationsUseRemovedNodes)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Map version cannot be published",
                Detail = "Every active location on the floor must have a node in the map version."
            };
            problem.Extensions["locationCodes"] = result.Locations;
            return Conflict(problem);
        }

        if (result.Error is PublishMapError.VersionNotDraft)
        {
            return Conflict(new ProblemDetails { Title = "Map version cannot be published", Detail = "Only a draft map version can be published." });
        }

        return Ok(result.Response);
    }

    [HttpPatch("{floorCode}/edges/{edgeId:guid}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateEdge(
        string floorCode,
        Guid edgeId,
        [FromBody] UpdateMapEdgeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mapService.UpdateEdgeAsync(userId, floorCode, edgeId, request, cancellationToken);
        if (result.Error is UpdateMapEdgeError.FloorNotFound or UpdateMapEdgeError.EdgeNotFound)
        {
            return NotFound(new ProblemDetails { Title = "Map edge not found", Detail = "The requested floor or map edge was not found." });
        }

        if (result.Error is UpdateMapEdgeError.EdgeNotPublished)
        {
            return Conflict(new ProblemDetails { Title = "Map edge cannot be updated", Detail = "Only edges in the published map can be updated." });
        }

        if (result.Error is UpdateMapEdgeError.InvalidRequest)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid map edge update", Detail = "A blocking update requires a reason of at most 500 characters." });
        }

        return NoContent();
    }

    [HttpPost("{floorCode}/maintenance")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(FloorMaintenanceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FloorMaintenanceResponse>> UpdateMaintenance(
        string floorCode,
        [FromBody] UpdateFloorMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await mapService.UpdateMaintenanceAsync(userId, floorCode, request, cancellationToken);
        if (result.Error is FloorMaintenanceError.FloorNotFound)
        {
            return NotFound(new ProblemDetails { Title = "Floor not found", Detail = $"Floor '{floorCode}' was not found." });
        }

        return Ok(result.Response);
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
