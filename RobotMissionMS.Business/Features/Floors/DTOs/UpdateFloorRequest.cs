using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Floors.DTOs;

public sealed class UpdateFloorRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Optional display-image path for the currently published map.</summary>
    [StringLength(500)]
    public string? BackgroundImagePath { get; init; }
}
