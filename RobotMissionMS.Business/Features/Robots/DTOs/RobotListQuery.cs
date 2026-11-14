using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Query parameters for GET /robots.
/// </summary>
public sealed class RobotListQuery
{
    /// <summary>Filter by zone/floor code, e.g. "F2".</summary>
    [StringLength(50)]
    public string? Floor { get; init; }

    /// <summary>Filter by robot type code, e.g. "MED_CART".</summary>
    [StringLength(50)]
    public string? Type { get; init; }

    /// <summary>
    /// Filter by robot state. Accepted values match the RobotState enum:
    /// INACTIVE, AVAILABLE, ON_TRIP, CHARGING, ERROR, MAINTENANCE.
    /// </summary>
    [StringLength(50)]
    public string? State { get; init; }

    /// <summary>Filter by connection status.</summary>
    public bool? IsConnected { get; init; }
}
