using RobotMissionMS.Business.Features.Floors.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class FloorServiceCollectionExtensions
{
    public static IServiceCollection AddFloorServices(this IServiceCollection services)
    {
        services.AddScoped<IFloorRepository, FloorRepository>();
        services.AddScoped<IFloorService, FloorService>();
        return services;
    }
}
