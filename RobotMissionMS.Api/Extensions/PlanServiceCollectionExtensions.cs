using RobotMissionMS.Business.Features.Plans.Services;
using RobotMissionMS.Business.Features.Trips.Services;

namespace RobotMissionMS.Api.Extensions;

public static class PlanServiceCollectionExtensions
{
    public static IServiceCollection AddPlanServices(this IServiceCollection services)
    {
        services.AddScoped<IPlanReadService, PlanReadService>();
        services.AddScoped<IPlanCommandService, PlanCommandService>();
        services.AddScoped<ITripExecutionService, TripExecutionService>();
        return services;
    }
}
