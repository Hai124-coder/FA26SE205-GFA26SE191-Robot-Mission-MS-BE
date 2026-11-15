using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Requests.DTOs;

public sealed class RequestPreviewRequest
{
    [Required, StringLength(50)]
    public string Department { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string ItemCode { get; init; } = string.Empty;

    [Range(1, 600)]
    public int Quantity { get; init; }

    [Required, StringLength(10)]
    public string Priority { get; init; } = string.Empty;

    /// <summary>Optional local operating-time deadline in HH:mm format.</summary>
    public string? Deadline { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }
}
