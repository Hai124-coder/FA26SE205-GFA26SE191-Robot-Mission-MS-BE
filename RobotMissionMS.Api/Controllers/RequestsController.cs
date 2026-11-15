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
[Authorize]
public sealed class RequestsController(IRequestPreviewService requestPreviewService, IRequestService requestService) : ControllerBase
{
    [HttpPost("preview")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
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
            RequestPreviewError.InvalidPriority => Problem(type: "REQUESTS_INVALID_PRIORITY", title: "Priority must be NORMAL, HIGH, or URGENT.", statusCode: 400),
            RequestPreviewError.InvalidDeadline => Problem(type: "REQUESTS_INVALID_DEADLINE", title: "Deadline must use HH:mm.", statusCode: 400),
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

    [HttpPost]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    [ProducesResponseType(typeof(RequestDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDetailResponse>> Create([FromBody] RequestPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)) return Unauthorized();
        var result = await requestService.CreateAsync(userId, request, cancellationToken);
        if (result.IsSuccess) return Created($"/api/v1/requests/{result.Response!.RequestCode}", result.Response);
        return PreviewFailure(result.Error!.Value);
    }

    [HttpGet("{requestCode}")]
    [Authorize(Policy = AuthPolicies.OperationsRead)]
    [ProducesResponseType(typeof(RequestDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailResponse>> Get(string requestCode, CancellationToken cancellationToken)
    {
        var isOperator = User.IsInRole("OPERATOR");
        var result = await requestService.GetByCodeAsync(requestCode, isOperator, User.FindFirstValue("rmms:floor"), cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            RequestReadError.InvalidRequestCode => Problem(type: "REQUESTS_INVALID_CODE", title: "The request code is invalid.", statusCode: 400),
            RequestReadError.NotFound => Problem(type: "REQUESTS_NOT_FOUND", title: "The request was not found.", statusCode: 404),
            RequestReadError.FloorForbidden => Problem(type: "FLOOR_FORBIDDEN", title: "The request is not on the operator floor.", statusCode: 403),
            _ => throw new InvalidOperationException("Request service returned an invalid read result.")
        };
    }

    private ObjectResult PreviewFailure(RequestPreviewError error) => error switch
    {
        RequestPreviewError.InvalidRequest => Problem(type: "REQUESTS_INVALID_PREVIEW", title: "The request is invalid.", statusCode: 400),
        RequestPreviewError.InvalidPriority => Problem(type: "REQUESTS_INVALID_PRIORITY", title: "Priority must be NORMAL, HIGH, or URGENT.", statusCode: 400),
        RequestPreviewError.InvalidDeadline => Problem(type: "REQUESTS_INVALID_DEADLINE", title: "Deadline must use HH:mm.", statusCode: 400),
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
        _ => throw new InvalidOperationException("Request service returned an invalid create result.")
    };
}
