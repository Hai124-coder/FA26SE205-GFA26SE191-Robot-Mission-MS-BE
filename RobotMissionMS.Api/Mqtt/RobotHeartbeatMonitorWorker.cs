using Microsoft.Extensions.Options;

namespace RobotMissionMS.Api.Mqtt;

public sealed class RobotHeartbeatMonitorWorker(
    RobotHeartbeatMonitorProcessor processor,
    IOptions<MqttOptions> options,
    ILogger<RobotHeartbeatMonitorWorker> logger) : BackgroundService
{
    private readonly MqttOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Robot heartbeat monitor is disabled because MQTT is disabled.");
            return;
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.HeartbeatTimeoutSeconds, 1, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var markedOffline = await processor.MarkStaleRobotsOfflineAsync(timeout, stoppingToken);
                if (markedOffline > 0)
                    logger.LogWarning("Marked {Count} robot(s) offline after no MQTT state for {TimeoutSeconds} seconds.", markedOffline, timeout.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Robot heartbeat monitor cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
