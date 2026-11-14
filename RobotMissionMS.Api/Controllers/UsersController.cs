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
