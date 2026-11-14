using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Items.DTOs;

public sealed class ItemListQuery
{
    [StringLength(50)] public string? MissionType { get; init; }
    public bool? Active { get; init; }
}
