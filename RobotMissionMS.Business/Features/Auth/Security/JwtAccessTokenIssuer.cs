using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RobotMissionMS.Business.Features.Auth.Configuration;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.Business.Features.Auth.Security;

public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly string issuer;
    private readonly string audience;
    private readonly int accessTokenMinutes;
    private readonly SigningCredentials signingCredentials;
    private readonly TimeProvider clock;

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        var settings = options.Value;
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(settings, new ValidationContext(settings), errors, validateAllProperties: true))
        {
            // Validation messages contain setting names, never the signing key.
            throw new OptionsValidationException(JwtOptions.SectionName, typeof(JwtOptions),
                errors.Select(error => error.ErrorMessage ?? "Invalid JWT configuration."));
        }

        // Copy validated settings so later mutations cannot change the issuer mid-request.
        issuer = settings.Issuer;
        audience = settings.Audience;
        accessTokenMinutes = settings.AccessTokenMinutes;
        signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        this.clock = clock;
    }

    public AccessTokenResult Issue(User user, Guid familyId)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id == Guid.Empty)
            throw new ArgumentException("A user ID is required.", nameof(user));
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Username);
        if (user.Role is null || string.IsNullOrWhiteSpace(user.Role.Name))
            throw new ArgumentException("The user's role must be loaded.", nameof(user));
        if (familyId == Guid.Empty)
            throw new ArgumentException("A session family ID is required.", nameof(familyId));

        // JWT NumericDate values use whole seconds. Keep the response expiry identical.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt.AddMinutes(accessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new Claim("name", user.Username),
            new Claim("role", user.Role.Name),
            new Claim("sid", familyId.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };
        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }
}
