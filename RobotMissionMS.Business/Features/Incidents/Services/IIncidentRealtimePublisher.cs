namespace RobotMissionMS.Business.Features.Incidents.Services;

public interface IIncidentRealtimePublisher
{
    Task PublishCreatedAsync(string? floor, IncidentRealtimeUpdate update, CancellationToken cancellationToken = default);
    Task PublishUpdatedAsync(string? floor, IncidentRealtimeUpdate update, CancellationToken cancellationToken = default);
}

public sealed record IncidentRealtimeUpdate(Guid Id, string? ScenarioCode, string AlertType, string Status, string? Phase, string Message, Guid? RobotId, Guid? TripId);
