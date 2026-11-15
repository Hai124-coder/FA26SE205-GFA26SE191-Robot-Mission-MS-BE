using RobotMissionMS.Business.Features.Tasks.Services;

namespace RobotMissionMS.Api.Extensions;

public static class TaskServiceCollectionExtensions
{
    public static IServiceCollection AddTaskServices(this IServiceCollection services)
    {
        services.AddScoped<ITaskReadService, TaskReadService>();
        return services;
    }
}
