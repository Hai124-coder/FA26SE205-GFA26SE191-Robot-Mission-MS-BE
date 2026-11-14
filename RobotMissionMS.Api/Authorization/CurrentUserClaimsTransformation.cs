using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Authorization;

/// <summary>
/// Replaces the JWT role with the user's current database role on every
/// authenticated request. A missing or inactive user becomes unauthenticated.
/// </summary>
public sealed class CurrentUserClaimsTransformation(RobotMissionDbContext db) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId))
            return new ClaimsPrincipal(new ClaimsIdentity());

        var user = await db.Users.AsNoTracking().Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Role?.Name))
            return new ClaimsPrincipal(new ClaimsIdentity());

        var identity = principal.Identities.FirstOrDefault(x => x.IsAuthenticated);
        if (identity is null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        foreach (var claim in identity.Claims.Where(x => x.Type == identity.RoleClaimType).ToArray())
            identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(identity.RoleClaimType, user.Role.Name));
        return principal;
    }
}
