using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RobotMissionMS.Business.Features.Auth.Configuration;
using RobotMissionMS.Business.Features.Auth.Security;
using RobotMissionMS.Business.Features.Auth.Services;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates authentication settings when the host starts. The JWT
    /// signing key must be supplied through User Secrets or environment variables.
    /// </summary>
    public static IServiceCollection AddAuthConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Configures bearer authentication from the already validated Jwt options.
    /// Claims retain the names issued by JwtAccessTokenIssuer: sub, name, role,
    /// sid, jti and iat.
    /// </summary>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.AdminOnly, policy =>
                policy.RequireAuthenticatedUser().RequireRole("ADMIN"));
            options.AddPolicy(AuthPolicies.OperatorOrAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole("OPERATOR", "ADMIN"));
            options.AddPolicy(AuthPolicies.OperationsRead, policy =>
                policy.RequireAuthenticatedUser().RequireRole("OPERATOR", "SUPERVISOR", "ADMIN"));
        });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddScoped<IClaimsTransformation, CurrentUserClaimsTransformation>();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var settings = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey)),
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        if (!Guid.TryParse(subject, out var userId))
                        {
                            context.Fail("The token subject is invalid.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<RobotMissionDbContext>();
                        var isActive = await db.Users.AsNoTracking()
                            .Where(user => user.Id == userId)
                            .Select(user => (bool?)user.IsActive)
                            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                        if (isActive != true)
                            context.Fail("The user is no longer active.");
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Registers Auth dependencies. The host must register RobotMissionDbContext
    /// and call AddAuthConfiguration before resolving Auth services.
    /// </summary>
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddOptions();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // Repository and service share the request's scoped DbContext.
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<IRefreshTokenGenerator, RefreshTokenGenerator>();

        return services;
    }
}
