namespace RobotMissionMS.Api.Mqtt;

public sealed class MqttOptions
{
    public bool Enabled { get; init; }
    public string? Host { get; init; }
    public int Port { get; init; } = 1883;
    public bool UseTls { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string ClientId { get; init; } = "rmms-backend";
    public int BatchSize { get; init; } = 10;
    public int PollIntervalSeconds { get; init; } = 2;
    public int LockSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 5;
}
