using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Incidents.DTOs;

public sealed class CreateIncidentRequest
{
    [Required, StringLength(20)]
    public string? ScenarioCode { get; init; }

    [Required, StringLength(2000)]
    public string? Message { get; init; }

    public Guid? ZoneId { get; init; }
    public Guid? RobotId { get; init; }
    public Guid? MissionId { get; init; }
    public Guid? TripId { get; init; }
    public IReadOnlyCollection<Guid>? AffectedMissionIds { get; init; }
}

public sealed class ApplyIncidentActionRequest
{
    [Required, StringLength(30)]
    public string? Action { get; init; }

    [StringLength(2000)]
    public string? Note { get; init; }
    public IReadOnlyCollection<Guid>? AffectedMissionIds { get; init; }
}

public sealed class IncidentActionResponse
{
    public Guid AlertId { get; init; }
    public string Status { get; init; } = null!;
    public string? Phase { get; init; }
    public string? SuggestedAction { get; init; }
}
