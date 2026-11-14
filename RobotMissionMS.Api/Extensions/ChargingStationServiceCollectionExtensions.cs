using RobotMissionMS.Business.Features.ChargingStations.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class ChargingStationServiceCollectionExtensions
{
    public static IServiceCollection AddChargingStationServices(this IServiceCollection services)
    {
        services.AddScoped<IChargingStationRepository, ChargingStationRepository>();
        services.AddScoped<IChargingStationService, ChargingStationService>();
        return services;
    }
}
