using RobotMissionMS.Business.Features.Dashboard.Services;

namespace RobotMissionMS.Api.Extensions;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
