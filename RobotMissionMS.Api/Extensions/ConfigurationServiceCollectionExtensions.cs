using RobotMissionMS.Business.Features.Configuration.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddConfigurationServices(this IServiceCollection services)
    {
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        return services;
    }
}
