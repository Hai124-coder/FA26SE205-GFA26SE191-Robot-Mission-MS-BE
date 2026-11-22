using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.ChargingSessions.DTOs;

public sealed class ChargingSessionListQuery
{
    public Guid? RobotId { get; init; }
    public string? Station { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
