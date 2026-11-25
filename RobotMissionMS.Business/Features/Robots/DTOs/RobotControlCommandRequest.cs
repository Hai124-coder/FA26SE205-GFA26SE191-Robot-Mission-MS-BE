using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>Immediate safety command for one connected robot.</summary>
public sealed class RobotControlCommandRequest
{
    [Required]
    [RegularExpression("^(STOP|RESUME)$", ErrorMessage = "Command must be STOP or RESUME.")]
    public string Command { get; init; } = string.Empty;
}
