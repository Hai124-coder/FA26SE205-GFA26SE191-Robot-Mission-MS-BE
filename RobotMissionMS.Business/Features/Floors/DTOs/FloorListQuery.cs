using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Floors.DTOs;

public sealed class FloorListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
