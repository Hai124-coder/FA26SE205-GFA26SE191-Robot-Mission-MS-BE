using RobotMissionMS.Business.Features.Robots.DTOs;

namespace RobotMissionMS.Business.Features.Robots.Services;

public interface IRobotTypeService
{
    Task<IReadOnlyList<RobotTypeResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the editable fields of a robot type identified by its TypeCode.
    /// </summary>
    Task<UpdateRobotTypeResult> UpdateAsync(
        string typeCode,
        UpdateRobotTypeRequest request,
        CancellationToken cancellationToken = default);
}
