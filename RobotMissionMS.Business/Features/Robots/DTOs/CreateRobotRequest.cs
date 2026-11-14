using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Request body for POST /robots.
/// Robot is created with State = INACTIVE, IsConnected = false.
/// Notes column is not in the current schema and is therefore omitted.
/// </summary>
public sealed class CreateRobotRequest
{
    /// <summary>Unique robot code, e.g. "MED-01".</summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string RobotCode { get; init; } = null!;

    /// <summary>TypeCode of the robot type, e.g. "MED_CART".</summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string RobotTypeCode { get; init; } = null!;

    /// <summary>Zone/floor code the robot belongs to, e.g. "F2".</summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string ZoneCode { get; init; } = null!;

    /// <summary>ID of the home charging station. Optional.</summary>
    public Guid? HomeChargingStationId { get; init; }
}
