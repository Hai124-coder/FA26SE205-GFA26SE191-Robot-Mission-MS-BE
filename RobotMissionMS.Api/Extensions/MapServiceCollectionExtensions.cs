using RobotMissionMS.Business.Features.Maps.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class MapServiceCollectionExtensions
{
    public static IServiceCollection AddMapServices(this IServiceCollection services)
    {
        services.AddScoped<IMapRepository, MapRepository>();
        services.AddScoped<IMapService, MapService>();
        return services;
    }
}
