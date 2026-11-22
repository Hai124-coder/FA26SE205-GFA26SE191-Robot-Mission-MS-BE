using RobotMissionMS.Business.Features.Incidents.Services;

namespace RobotMissionMS.Api.Extensions;

public static class IncidentServiceCollectionExtensions
{
    public static IServiceCollection AddIncidentServices(this IServiceCollection services)
    {
        services.AddScoped<IIncidentCommandService, IncidentCommandService>();
        services.AddScoped<IIncidentReadService, IncidentReadService>();
        return services;
    }
}
