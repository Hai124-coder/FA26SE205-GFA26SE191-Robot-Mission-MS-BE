using RobotMissionMS.Business.Features.Requests.DTOs;

namespace RobotMissionMS.Business.Features.Requests.Services;

public interface IRequestService
{
    Task<RequestCreateResult> CreateAsync(Guid operatorUserId, RequestPreviewRequest request,
        CancellationToken cancellationToken = default);
    Task<RequestReadResult> GetByCodeAsync(string requestCode, bool operatorOnly, string? operatorFloor,
        CancellationToken cancellationToken = default);
}
