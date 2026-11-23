using RobotMissionMS.Business.Features.Incidents.Services;

namespace RobotMissionMS.Api.Realtime;

public sealed class OperationsIncidentRealtimePublisher(IOperationsRealtimePublisher publisher) : IIncidentRealtimePublisher
{
    public Task PublishCreatedAsync(string? floor, IncidentRealtimeUpdate update, CancellationToken cancellationToken = default) => publisher.PublishAsync("incident.created", floor, update, cancellationToken);
    public Task PublishUpdatedAsync(string? floor, IncidentRealtimeUpdate update, CancellationToken cancellationToken = default) => publisher.PublishAsync("incident.updated", floor, update, cancellationToken);
}
