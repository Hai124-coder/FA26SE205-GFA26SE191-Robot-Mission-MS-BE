using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Request body for PUT /robot-types/{type}.
/// Only fields that exist in the current DB schema are included.
/// Payload/dimension columns are not yet in the schema and are therefore omitted.
/// </summary>
public sealed class UpdateRobotTypeRequest
{
    /// <summary>Display name of the robot type.</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Number of cargo slots. Must be 0 for schedule-only types (WASTE_CART, CLEAN_BOT).
    /// Must be ≥ 1 for batch-delivery types (MED_CART, SUPPLY_CART).
    /// </summary>
    [Range(0, 20)]
    public int Slots { get; init; }

    /// <summary>Hex colour used by the frontend map view, e.g. "#2563EB".</summary>
    [Required]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "ColorHex must be a 6-digit hex colour, e.g. #2563EB.")]
    public string ColorHex { get; init; } = null!;

    /// <summary>Optional free-text description.</summary>
    [StringLength(500)]
    public string? Description { get; init; }
}
