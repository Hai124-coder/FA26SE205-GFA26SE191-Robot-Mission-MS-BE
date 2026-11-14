using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Floors.DTOs;

public sealed class CreateFloorRequest
{
    [Required, StringLength(20, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}
