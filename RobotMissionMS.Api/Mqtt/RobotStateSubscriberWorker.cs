using System.Text.Json;
using System.Text.Json.Serialization;
using MQTTnet;
using MQTTnet.Protocol;
using Microsoft.Extensions.Options;

namespace RobotMissionMS.Api.Mqtt;

/// <summary>Receives, validates, and persists inbound robot-state MQTT messages.</summary>
public sealed class RobotStateSubscriberWorker(IOptions<MqttOptions> options, RobotStateTelemetryProcessor processor, ILogger<RobotStateSubscriberWorker> logger) : BackgroundService
{
    private readonly MqttOptions settings = options.Value;
    private readonly IMqttClient client = new MqttClientFactory().CreateMqttClient();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled) { logger.LogInformation("MQTT state subscriber is disabled."); return; }
        if (string.IsNullOrWhiteSpace(settings.Host)) { logger.LogError("MQTT state subscriber is enabled but Mqtt:Host is empty."); return; }
        client.ApplicationMessageReceivedAsync += HandleAsync;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    var builder = new MqttClientOptionsBuilder().WithClientId($"{settings.ClientId}-state").WithTcpServer(settings.Host, settings.Port).WithCleanSession(true);
                    if (!string.IsNullOrWhiteSpace(settings.Username)) builder.WithCredentials(settings.Username, settings.Password);
                    if (settings.UseTls) builder.WithTlsOptions(tls => tls.UseTls());
                    await client.ConnectAsync(builder.Build(), stoppingToken);
                    await client.SubscribeAsync("rmms/v1/+/state", MqttQualityOfServiceLevel.AtLeastOnce, stoppingToken);
                    logger.LogInformation("Subscribed to MQTT robot state at {Host}:{Port}.", settings.Host, settings.Port);
                }
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "MQTT state subscriber connection failed; retrying."); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    private async Task HandleAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var payload = args.ApplicationMessage.ConvertPayloadToString();
        try
        {
            var state = JsonSerializer.Deserialize<RobotStateMessage>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (!Valid(args.ApplicationMessage.Topic, state)) { logger.LogWarning("Rejected invalid MQTT robot state on {Topic}.", args.ApplicationMessage.Topic); return; }
            var result = await processor.ProcessAsync(state!.ToTelemetry());
            if (result == RobotStateTelemetryProcessResult.RobotNotFound)
            {
                logger.LogWarning("Ignored MQTT state for unknown robot {RobotId}.", state.RobotId);
                return;
            }
            logger.LogInformation("Persisted MQTT state for robot {RobotId}: {Status} at ({X}, {Y}), battery {Battery}.", state.RobotId, state.Status, state.X, state.Y, state.Battery);
        }
        catch (JsonException exception) { logger.LogWarning(exception, "Rejected malformed MQTT robot state on {Topic}.", args.ApplicationMessage.Topic); }
        catch (Exception exception) { logger.LogError(exception, "Failed to persist MQTT robot state on {Topic}.", args.ApplicationMessage.Topic); }
    }

    private static bool Valid(string topic, RobotStateMessage? state) => state is not null && !string.IsNullOrWhiteSpace(state.RobotId) && topic == $"rmms/v1/{state.RobotId}/state" && state.Timestamp != default && state.Battery is >= 0 and <= 100 && state.Speed >= 0 && state.Status is "IDLE" or "MOVING" or "AT_STOP" or "CHARGING" or "ERROR";

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { if (client.IsConnected) await client.DisconnectAsync(); }
        finally { client.Dispose(); await base.StopAsync(cancellationToken); }
    }

    private sealed record RobotStateMessage(string RobotId, [property: JsonPropertyName("ts")] DateTimeOffset Timestamp, double X, double Y, double Theta, int Battery, double Speed, string Status, string? OrderId, string? NextNodeId, int? AtStopSeq)
    {
        public RobotStateTelemetry ToTelemetry() => new(RobotId, Timestamp, X, Y, Theta, Battery, Speed, Status, OrderId, NextNodeId, AtStopSeq);
    }
}
