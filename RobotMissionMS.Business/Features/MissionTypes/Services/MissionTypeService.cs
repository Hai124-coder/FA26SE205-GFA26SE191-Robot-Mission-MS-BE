using RobotMissionMS.Business.Features.MissionTypes.DTOs;
using System.ComponentModel.DataAnnotations;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.MissionTypes.Services;

public sealed class MissionTypeService(IMissionTypeRepository repository) : IMissionTypeService
{
    public async Task<IReadOnlyList<MissionTypeResponse>> GetMissionTypesAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetMissionTypesAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<UpdateMissionTypeResult> UpdateAsync(Guid userId, string code, UpdateMissionTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code)
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)
            || !TryParsePickupKind(request.PickupKind, out var pickupKind))
            return UpdateMissionTypeResult.Failure(UpdateMissionTypeError.InvalidRequest);
        var missionType = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (missionType is null) return UpdateMissionTypeResult.Failure(UpdateMissionTypeError.NotFound);
        var row = await repository.UpdateAsync(missionType.Id, request.DepartDelaySeconds, request.WaitTimeoutSeconds,
            request.BasePriority, pickupKind, userId, DateTime.UtcNow, cancellationToken);
        return UpdateMissionTypeResult.Success(ToResponse(row));
    }

    private static MissionTypeResponse ToResponse(MissionTypeCatalogRow row) => new()
        {
            Id = row.Id, Code = row.Code, Name = row.Name, RobotType = row.RobotTypeCode, RobotTypeName = row.RobotTypeName,
            Mode = ToName(row.Mode), PickupKind = row.PickupKind is null ? null : ToName(row.PickupKind.Value),
            UsesSlotSensor = row.UsesSlotSensor, DepartDelaySeconds = row.DepartDelaySec,
            WaitTimeoutSeconds = row.WaitTimeoutSec, BasePriority = row.BasePriority
        };

    private static bool TryParsePickupKind(string? value, out LocationKind? kind)
    {
        kind = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        kind = value.Trim().ToUpperInvariant() switch
        {
            "PHARMACY" => LocationKind.Pharmacy, "STORAGE" => LocationKind.Storage, "SPECIMEN" => LocationKind.Specimen,
            "DEPT" => LocationKind.Dept, "WASTE_STATION" => LocationKind.WasteStation, "CHARGER" => LocationKind.Charger,
            "CLEAN_STATION" => LocationKind.CleanStation, "WASTE_POINT" => LocationKind.WastePoint, "CORRIDOR" => LocationKind.Corridor,
            _ => null
        };
        return kind is not null;
    }

    private static string ToName(MissionMode value) => value == MissionMode.Batch ? "BATCH" : "SCHEDULE";
    private static string ToName(LocationKind value) => value switch
    {
        LocationKind.Pharmacy => "PHARMACY", LocationKind.Storage => "STORAGE", LocationKind.Specimen => "SPECIMEN",
        LocationKind.Dept => "DEPT", LocationKind.WasteStation => "WASTE_STATION", LocationKind.Charger => "CHARGER",
        LocationKind.CleanStation => "CLEAN_STATION", LocationKind.WastePoint => "WASTE_POINT", LocationKind.Corridor => "CORRIDOR",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
