using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Locations.DTOs;

public sealed class UpdateLocationStatusRequest
{
    [Required] public bool? Active { get; init; }
}
