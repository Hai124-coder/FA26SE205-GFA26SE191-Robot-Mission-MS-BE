using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.MissionTypes.DTOs;

public sealed class UpdateMissionTypeRequest
{
    [Range(0, 3600)] public int DepartDelaySeconds { get; init; }
    [Range(0, 86400)] public int WaitTimeoutSeconds { get; init; }
    [Range(0, 100000)] public int BasePriority { get; init; }
    [StringLength(50)] public string? PickupKind { get; init; }
}
