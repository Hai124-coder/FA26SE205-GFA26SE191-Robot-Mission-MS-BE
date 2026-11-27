using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RobotMissionMS.Api.Authorization;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AllowedPaths = ["/api/v1/auth/change-password", "/api/v1/auth/me", "/api/v1/auth/logout"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirstValue("rmms:must-change-password") == "true"
            && !AllowedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Type = "PASSWORD_CHANGE_REQUIRED", Title = "A password change is required before using this resource.", Status = StatusCodes.Status403Forbidden });
            return;
        }
        await next(context);
    }
}
