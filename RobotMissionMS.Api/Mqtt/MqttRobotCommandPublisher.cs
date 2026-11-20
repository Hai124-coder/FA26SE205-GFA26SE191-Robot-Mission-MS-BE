using MQTTnet;
using MQTTnet.Protocol;
using Microsoft.Extensions.Options;

namespace RobotMissionMS.Api.Mqtt;

public sealed class MqttRobotCommandPublisher(IOptions<MqttOptions> options, ILogger<MqttRobotCommandPublisher> logger) : IRobotCommandPublisher, IAsyncDisposable
{
    private readonly MqttOptions settings = options.Value;
    private readonly IMqttClient client = new MqttClientFactory().CreateMqttClient();
    private readonly SemaphoreSlim connectionGate = new(1, 1);

    public async Task PublishAsync(string topic, string payloadJson, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);
        var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payloadJson)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build();
        await client.PublishAsync(message, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (client.IsConnected) return;
        if (string.IsNullOrWhiteSpace(settings.Host)) throw new InvalidOperationException("MQTT host is not configured.");
        await connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (client.IsConnected) return;
            var builder = new MqttClientOptionsBuilder().WithClientId(settings.ClientId).WithTcpServer(settings.Host, settings.Port).WithCleanSession(true);
            if (!string.IsNullOrWhiteSpace(settings.Username)) builder.WithCredentials(settings.Username, settings.Password);
            if (settings.UseTls) builder.WithTlsOptions(tls => tls.UseTls());
            await client.ConnectAsync(builder.Build(), cancellationToken);
            logger.LogInformation("Connected MQTT publisher to {Host}:{Port}.", settings.Host, settings.Port);
        }
        finally { connectionGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        try { if (client.IsConnected) await client.DisconnectAsync(); }
        finally { connectionGate.Dispose(); client.Dispose(); }
    }
}
