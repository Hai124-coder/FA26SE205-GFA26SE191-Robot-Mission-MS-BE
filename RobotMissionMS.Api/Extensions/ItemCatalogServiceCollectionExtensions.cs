using RobotMissionMS.Business.Features.Items.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class ItemCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddItemCatalogServices(this IServiceCollection services)
    {
        services.AddScoped<IItemCatalogRepository, ItemCatalogRepository>();
        services.AddScoped<IItemCatalogService, ItemCatalogService>();
        return services;
    }
}
