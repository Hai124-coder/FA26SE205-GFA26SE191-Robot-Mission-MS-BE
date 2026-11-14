using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Robots.Services;

public sealed class RobotTypeService(IRobotTypeRepository repository) : IRobotTypeService
{
    public async Task<IReadOnlyList<RobotTypeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetAllAsync(cancellationToken);

        return rows
            .Select(r => new RobotTypeResponse(
                r.Id,
                r.TypeCode,
                r.Name,
                r.Slots,
                r.ColorHex,
                r.Description))
            .ToList();
    }
}
