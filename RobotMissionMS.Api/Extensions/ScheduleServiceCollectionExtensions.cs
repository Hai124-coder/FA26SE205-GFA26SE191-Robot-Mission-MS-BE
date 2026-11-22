using RobotMissionMS.Business.Features.Schedules.Services;
using RobotMissionMS.Api.Schedules;

namespace RobotMissionMS.Api.Extensions;

public static class ScheduleServiceCollectionExtensions
{
    public static IServiceCollection AddScheduleServices(this IServiceCollection services)
    {
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<ScheduledRouteDispatchService>();
        services.AddScoped<ScheduleRunProcessor>();
        services.AddHostedService<ScheduleWorker>();
        return services;
    }
}
