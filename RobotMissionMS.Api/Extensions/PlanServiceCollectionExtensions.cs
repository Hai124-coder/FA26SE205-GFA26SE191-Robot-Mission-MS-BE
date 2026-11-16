using RobotMissionMS.Business.Features.Plans.Services;

namespace RobotMissionMS.Api.Extensions;

public static class PlanServiceCollectionExtensions
{
    public static IServiceCollection AddPlanServices(this IServiceCollection services)
    {
        services.AddScoped<IPlanReadService, PlanReadService>();
        services.AddScoped<IPlanCommandService, PlanCommandService>();
        return services;
    }
}
