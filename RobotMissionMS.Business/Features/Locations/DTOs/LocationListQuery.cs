using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Locations.DTOs;

public sealed class LocationListQuery
{
    [StringLength(50)]
    public string? Floor { get; init; }

    [StringLength(50)]
    public string? Kind { get; init; }
}
