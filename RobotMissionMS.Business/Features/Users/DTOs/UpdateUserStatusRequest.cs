using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Users.DTOs;

public sealed class UpdateUserStatusRequest
{
    [Required]
    public bool? Active { get; init; }
}
