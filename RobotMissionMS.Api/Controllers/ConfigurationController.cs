using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Configuration.DTOs;
using RobotMissionMS.Business.Features.Configuration.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/config")]
[Authorize]
public sealed class ConfigurationController(IConfigurationService configurationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigurationResponse>> GetConfiguration(CancellationToken cancellationToken) =>
        Ok(await configurationService.GetAsync(cancellationToken));

    [HttpPut]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigurationResponse>> UpdateConfiguration([FromBody] UpdateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)) return Unauthorized();
        var result = await configurationService.UpdateAsync(userId, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        var details = new ProblemDetails { Type = "CONFIG_INVALID_REQUEST", Title = "The configuration request is invalid.", Status = 400 };
        details.Extensions["errors"] = result.Errors;
        return BadRequest(details);
    }
}
