using RobotMissionMS.Business.Features.Requests.Services;
using RobotMissionMS.DataAccess.Repositories.Implementations;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Api.Extensions;

public static class RequestPreviewServiceCollectionExtensions
{
    public static IServiceCollection AddRequestPreviewServices(this IServiceCollection services)
    {
        services.AddScoped<IRequestPreviewRepository, RequestPreviewRepository>();
        services.AddScoped<IRequestPreviewService, RequestPreviewService>();
        return services;
    }
}
