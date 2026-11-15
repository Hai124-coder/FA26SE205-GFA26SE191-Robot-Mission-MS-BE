using RobotMissionMS.Business.Features.Requests.DTOs;

namespace RobotMissionMS.Business.Features.Requests.Services;

public interface IRequestPreviewService
{
    Task<RequestPreviewResult> PreviewAsync(Guid operatorUserId, RequestPreviewRequest request,
        CancellationToken cancellationToken = default);
}
