using RobotMissionMS.Business.Features.Incidents.DTOs;

namespace RobotMissionMS.Business.Features.Incidents.Services;

public interface IIncidentCommandService
{
    Task<IncidentCommandResult> CreateAsync(Guid? actorUserId, CreateIncidentRequest request, CancellationToken cancellationToken = default);
    Task<IncidentCommandResult> ApplyActionAsync(Guid alertId, Guid actorUserId, string floor, ApplyIncidentActionRequest request, CancellationToken cancellationToken = default);
}

public enum IncidentCommandError
{
    InvalidRequest,
    ScenarioNotSupported,
    AlertNotFound,
    AlertResolved,
    ActionNotAllowed,
    InvalidPhase,
    ActorNotFound,
    FloorForbidden,
    RelatedEntityNotFound,
    RelatedMissionNotFound,
    MissionNotActionable,
}

public sealed class IncidentCommandResult
{
    private IncidentCommandResult(IncidentActionResponse? response, IncidentCommandError? error) { Response = response; Error = error; }
    public IncidentActionResponse? Response { get; }
    public IncidentCommandError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static IncidentCommandResult Success(IncidentActionResponse response) => new(response, null);
    public static IncidentCommandResult Failure(IncidentCommandError error) => new(null, error);
}
