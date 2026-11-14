using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Locations.DTOs;

public sealed class UpdateLocationRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(20, MinimumLength = 1)] public string Floor { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string Kind { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string MapNodeCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsRestrictedArea { get; init; }
    [StringLength(50)] public string? RestrictedHours { get; init; }
}
