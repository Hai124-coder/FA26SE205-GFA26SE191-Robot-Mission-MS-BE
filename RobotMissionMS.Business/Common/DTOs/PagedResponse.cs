namespace RobotMissionMS.Business.Common.DTOs;

/// <summary>
/// Standard response envelope for collection endpoints.
/// </summary>
public sealed class PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int Total { get; init; }
}
