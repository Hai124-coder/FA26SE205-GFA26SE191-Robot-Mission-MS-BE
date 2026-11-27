using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Locations.DTOs;

public sealed class LocationListQuery
{
    [StringLength(50)]
    public string? Floor { get; init; }

    [StringLength(50)]
    public string? Kind { get; init; }

    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
