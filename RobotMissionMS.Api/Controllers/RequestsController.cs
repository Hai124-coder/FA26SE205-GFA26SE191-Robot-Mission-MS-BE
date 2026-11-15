using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Requests.DTOs;
using RobotMissionMS.Business.Features.Requests.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/requests")]
[Authorize(Policy = AuthPolicies.OperatorOnly)]
public sealed class RequestsController(IRequestPreviewService requestPreviewService) : ControllerBase
{
    [HttpPost("preview")]
    [ProducesResponseType(typeof(RequestPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestPreviewResponse>> Preview([FromBody] RequestPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)) return Unauthorized();
        var result = await requestPreviewService.PreviewAsync(userId, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            RequestPreviewError.InvalidRequest => Problem(type: "REQUESTS_INVALID_PREVIEW", title: "The preview request is invalid.", statusCode: 400),
            RequestPreviewError.OperatorFloorNotConfigured => Problem(type: "OPERATOR_FLOOR_NOT_CONFIGURED", title: "The operator does not have a floor assignment.", statusCode: 409),
            RequestPreviewError.OperatorFloorInactive => Problem(type: "FLOORS_INACTIVE", title: "The operator floor is inactive.", statusCode: 409),
            RequestPreviewError.DepartmentNotFound => Problem(type: "DEPARTMENTS_NOT_FOUND", title: "The department was not found.", statusCode: 400),
            RequestPreviewError.DepartmentInactive => Problem(type: "DEPARTMENTS_INACTIVE", title: "The department is inactive.", statusCode: 409),
            RequestPreviewError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", title: "The department is not on the operator floor.", statusCode: 403),
            RequestPreviewError.ItemNotFound => Problem(type: "ITEMS_NOT_FOUND", title: "The item was not found.", statusCode: 400),
            RequestPreviewError.ItemInactive => Problem(type: "ITEMS_INACTIVE", title: "The item is inactive.", statusCode: 409),
            RequestPreviewError.MissionTypeInactive => Problem(type: "MISSION_TYPES_INACTIVE", title: "The item mission type is inactive.", statusCode: 409),
            RequestPreviewError.MissionTypeNotAvailableForManualRequest => Problem(type: "MISSION_TYPES_MANUAL_REQUEST_FORBIDDEN", title: "The mission type does not accept manual requests.", statusCode: 409),
            RequestPreviewError.PickupLocationNotConfigured => Problem(type: "PICKUP_LOCATION_NOT_CONFIGURED", title: "No pickup location is configured for this mission type on the operator floor.", statusCode: 409),
            RequestPreviewError.ItemWeightNotConfigured => Problem(type: "ITEMS_WEIGHT_NOT_CONFIGURED", title: "The item capacity or weight is not configured.", statusCode: 409),
            _ => throw new InvalidOperationException("Request preview service returned an invalid result.")
        };
    }
}
