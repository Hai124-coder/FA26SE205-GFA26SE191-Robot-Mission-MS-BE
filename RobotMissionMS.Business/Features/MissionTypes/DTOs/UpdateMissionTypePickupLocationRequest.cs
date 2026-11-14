using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.MissionTypes.DTOs;

public sealed class UpdateMissionTypePickupLocationRequest
{
    [Required, StringLength(20, MinimumLength = 1)] public string Floor { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string LocationCode { get; init; } = string.Empty;
}
