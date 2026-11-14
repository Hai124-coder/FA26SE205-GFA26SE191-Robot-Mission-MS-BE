using RobotMissionMS.Business.Features.Robots.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class RobotServiceCollectionExtensions
{
    public static IServiceCollection AddRobotServices(this IServiceCollection services)
    {
        services.AddScoped<IRobotTypeRepository, RobotTypeRepository>();
        services.AddScoped<IRobotTypeService, RobotTypeService>();
        return services;
    }
}
