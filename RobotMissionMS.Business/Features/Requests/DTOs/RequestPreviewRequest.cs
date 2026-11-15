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
}
