using RobotMissionMS.Api.Mqtt;

namespace RobotMissionMS.Api.Extensions;

public static class MqttServiceCollectionExtensions
{
    public static IServiceCollection AddMqttOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MqttOptions>(configuration.GetSection("Mqtt"));
        services.AddSingleton<IRobotCommandPublisher, MqttRobotCommandPublisher>();
        services.AddHostedService<RobotCommandOutboxWorker>();
        return services;
    }
}
