using RobotMissionMS.Business.Features.Locations.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class LocationServiceCollectionExtensions
{
    public static IServiceCollection AddLocationServices(this IServiceCollection services)
    {
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationService, LocationService>();
        return services;
    }
}
