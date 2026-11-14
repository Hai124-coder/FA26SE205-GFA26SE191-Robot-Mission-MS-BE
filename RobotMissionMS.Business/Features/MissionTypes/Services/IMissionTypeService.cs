using RobotMissionMS.Business.Features.MissionTypes.DTOs;

namespace RobotMissionMS.Business.Features.MissionTypes.Services;

public interface IMissionTypeService
{
    Task<IReadOnlyList<MissionTypeResponse>> GetMissionTypesAsync(CancellationToken cancellationToken = default);
    Task<UpdateMissionTypeResult> UpdateAsync(Guid userId, string code, UpdateMissionTypeRequest request,
        CancellationToken cancellationToken = default);
}
