using RobotMissionMS.Api.Imports;

namespace RobotMissionMS.Api.Extensions;

public static class ImportServiceCollectionExtensions
{
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        services.AddScoped<IImportTemplateService, ImportTemplateService>();
        return services;
    }
}
