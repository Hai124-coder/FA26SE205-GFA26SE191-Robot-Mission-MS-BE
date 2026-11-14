using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Business.Features.Auth.DTOs;
using RobotMissionMS.Business.Features.Auth.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            LoginError.InvalidRequest => Problem(
                type: "AUTH_INVALID_REQUEST",
                title: "The login request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            LoginError.InvalidCredentials => Problem(
                type: "AUTH_INVALID_CREDENTIALS",
                title: "Invalid username or password.",
                statusCode: StatusCodes.Status401Unauthorized),
            LoginError.AccountInactive => Problem(
                type: "AUTH_ACCOUNT_INACTIVE",
                title: "This account is inactive.",
                statusCode: StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException("Auth service returned an invalid login result.")
        };
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Response!);

        return result.Error switch
        {
            RefreshError.InvalidRequest => Problem(
                type: "AUTH_INVALID_REFRESH_REQUEST",
                title: "The refresh request is invalid.",
                statusCode: StatusCodes.Status400BadRequest),
            RefreshError.InvalidRefreshToken => Problem(
                type: "AUTH_INVALID_REFRESH_TOKEN",
                title: "The refresh token is invalid or expired.",
                statusCode: StatusCodes.Status401Unauthorized),
            RefreshError.AccountInactive => Problem(
                type: "AUTH_ACCOUNT_INACTIVE",
                title: "This account is inactive.",
                statusCode: StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException("Auth service returned an invalid refresh result.")
        };
    }
}
