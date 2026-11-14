using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Robots.DTOs;

public sealed class UpdateRobotTypeRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 20)]
    public int Slots { get; init; }

    [Required]
    [StringLength(20)]
    public string ColorHex { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }
}
