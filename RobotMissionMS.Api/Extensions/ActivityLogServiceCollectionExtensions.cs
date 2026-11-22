using RobotMissionMS.Business.Features.ActivityLogs.Services;

namespace RobotMissionMS.Api.Extensions;

public static class ActivityLogServiceCollectionExtensions
{
    public static IServiceCollection AddActivityLogServices(this IServiceCollection services)
    {
        services.AddScoped<IActivityLogService, ActivityLogService>();
        return services;
    }
}
