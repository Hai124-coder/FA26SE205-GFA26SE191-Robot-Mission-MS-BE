using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Users.DTOs;
using RobotMissionMS.Business.Features.Users.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(typeof(UserListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserListItemResponse>> ResetUserPassword(
        Guid id,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var resetByUserId))
            return Unauthorized();

        var result = await userService.ResetUserPasswordAsync(resetByUserId, id, new ResetUserPasswordRequest(), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            ResetUserPasswordError.InvalidRequest => Problem(
                type: "USERS_INVALID_PASSWORD_RESET_REQUEST",
                title: "The password reset request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            ResetUserPasswordError.UserNotFound => Problem(
                type: "USERS_NOT_FOUND",
                title: "The requested user was not found.",
                statusCode: StatusCodes.Status404NotFound),
            ResetUserPasswordError.CannotResetSelf => Problem(type: "ADMIN_CANNOT_RESET_SELF", statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException("User service returned an invalid password-reset result.")
        };
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(UserListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserListItemResponse>> UpdateUserStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var updatedByUserId))
            return Unauthorized();

        var result = await userService.UpdateUserStatusAsync(updatedByUserId, id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            UpdateUserStatusError.InvalidRequest => Problem(
                type: "USERS_INVALID_STATUS_REQUEST",
                title: "The user status request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            UpdateUserStatusError.UserNotFound => Problem(
                type: "USERS_NOT_FOUND",
                title: "The requested user was not found.",
                statusCode: StatusCodes.Status404NotFound),
            _ => throw new InvalidOperationException("User service returned an invalid status result.")
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserListItemResponse>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var updatedByUserId))
            return Unauthorized();

        var result = await userService.UpdateUserAsync(updatedByUserId, id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            UpdateUserError.InvalidRequest => Problem(
                type: "USERS_INVALID_UPDATE_REQUEST",
                title: "The user update request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            UpdateUserError.UserNotFound => Problem(
                type: "USERS_NOT_FOUND",
                title: "The requested user was not found.",
                statusCode: StatusCodes.Status404NotFound),
            UpdateUserError.RoleNotFound => Problem(
                type: "USERS_ROLE_NOT_FOUND",
                title: "The requested role does not exist.",
                statusCode: StatusCodes.Status400BadRequest),
            UpdateUserError.OperatorFloorRequired => Problem(type: "USERS_OPERATOR_FLOOR_REQUIRED", title: "An Operator must have a floor.", statusCode: 400),
            UpdateUserError.NonOperatorFloorForbidden => Problem(type: "USERS_NON_OPERATOR_FLOOR_FORBIDDEN", title: "Only an Operator can have a floor.", statusCode: 400),
            UpdateUserError.FloorNotFound => Problem(type: "FLOORS_NOT_FOUND", title: "The floor was not found.", statusCode: 400),
            UpdateUserError.FloorInactive => Problem(type: "FLOORS_INACTIVE", title: "The floor is inactive.", statusCode: 409),
            _ => throw new InvalidOperationException("User service returned an invalid update result.")
        };
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserListItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserListItemResponse>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var createdByUserId))
            return Unauthorized();

        var result = await userService.CreateUserAsync(createdByUserId, request, cancellationToken);
        if (result.IsSuccess)
        {
            return Created($"/api/v1/users/{result.Response!.Id:D}", result.Response);
        }

        return result.Error switch
        {
            CreateUserError.InvalidRequest => Problem(
                type: "USERS_INVALID_CREATE_REQUEST",
                title: "The user creation request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            CreateUserError.UsernameAlreadyExists => Problem(
                type: "USERS_USERNAME_ALREADY_EXISTS",
                title: "The username is already in use.",
                statusCode: StatusCodes.Status409Conflict),
            CreateUserError.RoleNotFound => Problem(
                type: "USERS_ROLE_NOT_FOUND",
                title: "The requested role does not exist.",
                statusCode: StatusCodes.Status400BadRequest),
            CreateUserError.OperatorFloorRequired => Problem(type: "USERS_OPERATOR_FLOOR_REQUIRED", title: "An Operator must have a floor.", statusCode: 400),
            CreateUserError.NonOperatorFloorForbidden => Problem(type: "USERS_NON_OPERATOR_FLOOR_FORBIDDEN", title: "Only an Operator can have a floor.", statusCode: 400),
            CreateUserError.FloorNotFound => Problem(type: "FLOORS_NOT_FOUND", title: "The floor was not found.", statusCode: 400),
            CreateUserError.FloorInactive => Problem(type: "FLOORS_INACTIVE", title: "The floor is inactive.", statusCode: 409),
            _ => throw new InvalidOperationException("User service returned an invalid create result.")
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<UserListItemResponse>>> GetUsers(
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetUsersAsync(query, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            UserListError.InvalidQuery => Problem(
                type: "USERS_INVALID_LIST_QUERY",
                title: "The user list query is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException("User service returned an invalid list result.")
        };
    }
}
