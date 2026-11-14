using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public interface IRobotTypeService
{
    Task<IReadOnlyList<RobotTypeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
