using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Incidents.DTOs;
using RobotMissionMS.Business.Features.Incidents.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/incidents")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class IncidentsController(IIncidentCommandService commands, IIncidentReadService incidents) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(RobotMissionMS.Business.Common.DTOs.PagedResponse<IncidentListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<RobotMissionMS.Business.Common.DTOs.PagedResponse<IncidentListItemResponse>>> List([FromQuery] IncidentListQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: StatusCodes.Status409Conflict);
        var result = await incidents.GetIncidentsAsync(query, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(IncidentStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IncidentStatsResponse>> Stats([FromQuery] IncidentStatsQuery query, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: StatusCodes.Status409Conflict);
        var result = await incidents.GetStatsAsync(query, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IncidentDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IncidentDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (OperatorWithoutFloor()) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: StatusCodes.Status409Conflict);
        var result = await incidents.GetIncidentAsync(id, OperatorFloor(), cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : ReadFailure(result.Error!.Value);
    }

    /// <summary>Performs one permitted action on an active incident.</summary>
    [HttpPost("{id:guid}/actions")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    [ProducesResponseType(typeof(IncidentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidentActionResponse>> ApplyAction(Guid id, [FromBody] ApplyIncidentActionRequest request, CancellationToken cancellationToken)
    {
        var floor = User.FindFirstValue("rmms:floor");
        if (string.IsNullOrWhiteSpace(floor)) return Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", statusCode: StatusCodes.Status409Conflict);
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)) return Unauthorized();

        var result = await commands.ApplyActionAsync(id, userId, floor, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    private string? OperatorFloor() => User.IsInRole("OPERATOR") ? User.FindFirstValue("rmms:floor") : null;
    private bool OperatorWithoutFloor() => User.IsInRole("OPERATOR") && string.IsNullOrWhiteSpace(OperatorFloor());
    private ActionResult ReadFailure(IncidentReadError error) => error switch
    {
        IncidentReadError.InvalidQuery => Problem(type: "INCIDENTS_INVALID_QUERY", statusCode: StatusCodes.Status400BadRequest),
        IncidentReadError.NotFound => Problem(type: "INCIDENT_NOT_FOUND", statusCode: StatusCodes.Status404NotFound),
        IncidentReadError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", statusCode: StatusCodes.Status403Forbidden),
        _ => throw new InvalidOperationException("Incident read service returned an invalid result.")
    };

    private ActionResult Failure(IncidentCommandError error) => error switch
    {
        IncidentCommandError.InvalidRequest => Problem(type: "INCIDENTS_INVALID_REQUEST", statusCode: StatusCodes.Status400BadRequest),
        IncidentCommandError.AlertNotFound => Problem(type: "INCIDENT_NOT_FOUND", statusCode: StatusCodes.Status404NotFound),
        IncidentCommandError.ActorNotFound => Problem(type: "INCIDENT_ACTOR_NOT_FOUND", statusCode: StatusCodes.Status401Unauthorized),
        IncidentCommandError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", statusCode: StatusCodes.Status403Forbidden),
        IncidentCommandError.RelatedEntityNotFound or IncidentCommandError.RelatedMissionNotFound => Problem(type: "INCIDENT_RELATED_RESOURCE_NOT_FOUND", statusCode: StatusCodes.Status404NotFound),
        IncidentCommandError.MissionNotActionable => Problem(type: "INCIDENT_MISSION_NOT_ACTIONABLE", statusCode: StatusCodes.Status409Conflict),
        IncidentCommandError.AlertResolved => Problem(type: "INCIDENT_ALREADY_RESOLVED", statusCode: StatusCodes.Status409Conflict),
        IncidentCommandError.ActionNotAllowed or IncidentCommandError.InvalidPhase => Problem(type: "ACTION_NOT_ALLOWED", statusCode: StatusCodes.Status409Conflict),
        IncidentCommandError.ScenarioNotSupported => Problem(type: "INCIDENT_SCENARIO_NOT_SUPPORTED", statusCode: StatusCodes.Status409Conflict),
        _ => throw new InvalidOperationException("Incident command service returned an invalid result.")
    };
}
