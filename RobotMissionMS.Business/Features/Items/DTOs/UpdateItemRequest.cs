using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Items.DTOs;

public sealed class UpdateItemRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string MissionType { get; init; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 1)] public string Unit { get; init; } = string.Empty;
    [Range(1, 100000)] public int MaxQtyPerSlot { get; init; }
    [StringLength(100)] public string? Packaging { get; init; }
    [Range(typeof(decimal), "0.001", "99999.999")] public decimal? KgPerUnit { get; init; }
    [Required, StringLength(20)] public string CapacityLimitReason { get; init; } = string.Empty;
    [StringLength(1000)] public string? Description { get; init; }
}
