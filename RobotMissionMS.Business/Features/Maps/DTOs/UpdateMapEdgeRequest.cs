using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Maps.DTOs;

public sealed class UpdateMapEdgeRequest
{
    public bool IsBlocked { get; init; }
    [StringLength(500)] public string? Reason { get; init; }
}
