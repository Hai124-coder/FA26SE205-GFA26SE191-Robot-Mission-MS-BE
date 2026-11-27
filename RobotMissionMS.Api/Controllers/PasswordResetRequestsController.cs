using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/password-reset-requests")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class PasswordResetRequestsController(RobotMissionDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var query = db.PasswordResetRequests.AsNoTracking().Include(value => value.User).ThenInclude(value => value.Role).Include(value => value.User.Zone).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PasswordResetRequestStatus>(status, true, out var parsed)) return Problem(type: "PASSWORD_RESET_REQUESTS_INVALID_QUERY", statusCode: 400);
            query = query.Where(value => value.Status == parsed);
        }
        else query = query.Where(value => value.Status == PasswordResetRequestStatus.Open);
        var rows = await query.OrderByDescending(value => value.RequestedAt).ToListAsync(cancellationToken);
        var items = rows.Select(value => new { id = value.Id, code = value.RequestCode, username = value.User.Username, fullName = value.User.FullName, role = value.User.Role.Name, floor = value.User.Zone?.Code, note = value.Note, status = value.Status.ToString().ToUpperInvariant(), requestedAt = value.RequestedAt, processedAt = value.ProcessedAt, processedByUserId = value.ProcessedByUserId });
        return Ok(items);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var adminId)) return Unauthorized();
        var request = await db.PasswordResetRequests.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (request is null) return Problem(type: "PASSWORD_RESET_REQUEST_NOT_FOUND", statusCode: 404);
        if (request.Status != PasswordResetRequestStatus.Open) return Problem(type: "PASSWORD_RESET_REQUEST_NOT_OPEN", statusCode: 409);
        var now = DateTime.UtcNow;
        request.Status = PasswordResetRequestStatus.Rejected;
        request.ProcessedAt = now;
        request.ProcessedByUserId = adminId;
        db.ActivityLogs.Add(new ActivityLog { UserId = adminId, Timestamp = now, Action = "PASSWORD_RESET_REQUEST_REJECTED", TargetObject = $"PasswordResetRequest:{id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
