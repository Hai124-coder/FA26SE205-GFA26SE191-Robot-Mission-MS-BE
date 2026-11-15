using RobotMissionMS.Business.Features.Requests.DTOs;

namespace RobotMissionMS.Business.Features.Requests.Services;

public enum RequestPreviewError
{
    InvalidRequest,
    InvalidPriority,
    InvalidDeadline,
    OperatorFloorNotConfigured,
    OperatorFloorInactive,
    DepartmentNotFound,
    DepartmentInactive,
    FloorForbidden,
    ItemNotFound,
    ItemInactive,
    MissionTypeInactive,
    MissionTypeNotAvailableForManualRequest,
    PickupLocationNotConfigured,
    ItemWeightNotConfigured
}

public sealed class RequestPreviewResult
{
    private RequestPreviewResult(RequestPreviewResponse? response, RequestPreviewError? error) => (Response, Error) = (response, error);
    public RequestPreviewResponse? Response { get; }
    public RequestPreviewError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static RequestPreviewResult Success(RequestPreviewResponse response) => new(response, null);
    public static RequestPreviewResult Failure(RequestPreviewError error) => new(null, error);
}
