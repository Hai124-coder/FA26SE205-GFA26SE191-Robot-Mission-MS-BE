namespace RobotMissionMS.Api.Mqtt;

public interface IRobotCommandPublisher
{
    Task PublishAsync(string topic, string payloadJson, CancellationToken cancellationToken);
}
