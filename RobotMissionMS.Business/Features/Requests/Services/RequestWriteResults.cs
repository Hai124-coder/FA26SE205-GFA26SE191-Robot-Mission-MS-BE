using RobotMissionMS.Business.Features.Requests.DTOs;

namespace RobotMissionMS.Business.Features.Requests.Services;

public enum RequestReadError { InvalidRequestCode, NotFound, FloorForbidden }

public sealed class RequestCreateResult
{
    private RequestCreateResult(RequestDetailResponse? response, RequestPreviewError? error) => (Response, Error) = (response, error);
    public RequestDetailResponse? Response { get; }
    public RequestPreviewError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static RequestCreateResult Success(RequestDetailResponse response) => new(response, null);
    public static RequestCreateResult Failure(RequestPreviewError error) => new(null, error);
}

public sealed class RequestReadResult
{
    private RequestReadResult(RequestDetailResponse? response, RequestReadError? error) => (Response, Error) = (response, error);
    public RequestDetailResponse? Response { get; }
    public RequestReadError? Error { get; }
    public bool IsSuccess => Response is not null;
    public static RequestReadResult Success(RequestDetailResponse response) => new(response, null);
    public static RequestReadResult Failure(RequestReadError error) => new(null, error);
}
