using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RobotMissionMS.Business.Features.Auth.Security;
using RobotMissionMS.Business.Features.Auth.Services;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers Auth dependencies. The host must register RobotMissionDbContext
    /// and configure JwtOptions/AuthOptions separately before using Auth.
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
