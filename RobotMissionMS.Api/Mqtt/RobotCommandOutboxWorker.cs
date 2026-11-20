using Microsoft.Extensions.Options;

namespace RobotMissionMS.Api.Mqtt;

public sealed class RobotCommandOutboxWorker(RobotCommandOutboxProcessor processor, IOptions<MqttOptions> options, ILogger<RobotCommandOutboxWorker> logger) : BackgroundService
{
    private readonly MqttOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled) { logger.LogInformation("MQTT outbox worker is disabled."); return; }
        if (string.IsNullOrWhiteSpace(settings.Host)) { logger.LogError("MQTT outbox worker is enabled but Mqtt:Host is empty."); return; }
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.PollIntervalSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var index = 0; index < Math.Clamp(settings.BatchSize, 1, 100); index++)
                    if (!await processor.ProcessOneAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "MQTT outbox worker cycle failed."); }
            await Task.Delay(interval, stoppingToken);
        }
    }

}
