using Microsoft.AspNetCore.SignalR;
namespace RobotMissionMS.Api.Realtime;
public interface IOperationsRealtimePublisher { Task PublishAsync(string type, string? floor, object data, CancellationToken ct = default); }
public sealed class OperationsRealtimePublisher(IHubContext<OperationsHub> hub) : IOperationsRealtimePublisher
{
    public Task PublishAsync(string type, string? floor, object data, CancellationToken ct = default)
    {
        var item = new OperationsEvent(Guid.NewGuid(), DateTime.UtcNow, type, floor, data);
        return string.IsNullOrWhiteSpace(floor) ? hub.Clients.All.SendAsync(type, item, ct) : hub.Clients.Group(OperationsHub.Group(floor)).SendAsync(type, item, ct);
    }
}
