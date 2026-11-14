using RobotMissionMS.Business.Features.MissionTypes.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class MissionTypeServiceCollectionExtensions
{
    public static IServiceCollection AddMissionTypeServices(this IServiceCollection services)
    {
        services.AddScoped<IMissionTypeRepository, MissionTypeRepository>();
        services.AddScoped<IMissionTypeService, MissionTypeService>();
        return services;
    }
}
