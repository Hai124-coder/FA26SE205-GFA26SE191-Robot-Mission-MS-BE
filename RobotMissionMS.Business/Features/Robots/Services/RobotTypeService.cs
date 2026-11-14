using System.ComponentModel.DataAnnotations;
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

    public async Task<UpdateRobotTypeResult> UpdateAsync(
        string typeCode,
        UpdateRobotTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(typeCode)
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.ColorHex))
            return UpdateRobotTypeResult.Failure(UpdateRobotTypeError.InvalidRequest);

        var existing = await repository.FindByTypeCodeAsync(typeCode, cancellationToken);
        if (existing is null)
            return UpdateRobotTypeResult.Failure(UpdateRobotTypeError.NotFound);

        await repository.UpdateAsync(
            existing.Id,
            request.Name.Trim(),
            request.Slots,
            request.ColorHex.Trim(),
            request.Description?.Trim(),
            cancellationToken);

        return UpdateRobotTypeResult.Success(new RobotTypeResponse(
            existing.Id,
            existing.TypeCode,
            request.Name.Trim(),
            request.Slots,
            request.ColorHex.Trim(),
            request.Description?.Trim()));
    }
}
