using RobotMissionMS.Business.Features.Items.DTOs;

namespace RobotMissionMS.Business.Features.Items.Services;

public enum CreateItemError { InvalidRequest, DuplicateCode, MissionTypeNotFound }
public enum UpdateItemError { InvalidRequest, NotFound, MissionTypeNotFound, MissionTypeChangeNotAllowed }
public enum UpdateItemStatusError { InvalidRequest, NotFound }

public sealed class CreateItemResult
{
    private CreateItemResult(ItemResponse? response, CreateItemError? error) { Response = response; Error = error; }
    public ItemResponse? Response { get; } public CreateItemError? Error { get; } public bool IsSuccess => Error is null;
    public static CreateItemResult Success(ItemResponse response) => new(response, null);
    public static CreateItemResult Failure(CreateItemError error) => new(null, error);
}

public sealed class UpdateItemResult
{
    private UpdateItemResult(ItemResponse? response, UpdateItemError? error) { Response = response; Error = error; }
    public ItemResponse? Response { get; } public UpdateItemError? Error { get; } public bool IsSuccess => Error is null;
    public static UpdateItemResult Success(ItemResponse response) => new(response, null);
    public static UpdateItemResult Failure(UpdateItemError error) => new(null, error);
}

public sealed class UpdateItemStatusResult
{
    private UpdateItemStatusResult(ItemResponse? response, UpdateItemStatusError? error) { Response = response; Error = error; }
    public ItemResponse? Response { get; } public UpdateItemStatusError? Error { get; } public bool IsSuccess => Error is null;
    public static UpdateItemStatusResult Success(ItemResponse response) => new(response, null);
    public static UpdateItemStatusResult Failure(UpdateItemStatusError error) => new(null, error);
}
