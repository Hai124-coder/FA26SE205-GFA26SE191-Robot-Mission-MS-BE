using RobotMissionMS.Business.Features.Reports.Services;

namespace RobotMissionMS.Api.Extensions;

public static class ReportServiceCollectionExtensions
{
    public static IServiceCollection AddReportServices(this IServiceCollection services)
    {
        services.AddScoped<IOperationsReportService, OperationsReportService>();
        return services;
    }
}
