using RobotMissionMS.Business.Features.Items.DTOs;

namespace RobotMissionMS.Business.Features.Items.Services;

public enum ItemListError { InvalidQuery }

public sealed class ItemListResult
{
    private ItemListResult(IReadOnlyList<ItemResponse>? response, ItemListError? error) { Response = response; Error = error; }
    public IReadOnlyList<ItemResponse>? Response { get; }
    public ItemListError? Error { get; }
    public bool IsSuccess => Error is null;
    public static ItemListResult Success(IReadOnlyList<ItemResponse> response) => new(response, null);
    public static ItemListResult Failure() => new(null, ItemListError.InvalidQuery);
}
