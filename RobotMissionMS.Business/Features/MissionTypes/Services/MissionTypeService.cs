using RobotMissionMS.Business.Features.MissionTypes.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.MissionTypes.Services;

public sealed class MissionTypeService(IMissionTypeRepository repository) : IMissionTypeService
{
    public async Task<IReadOnlyList<MissionTypeResponse>> GetMissionTypesAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetMissionTypesAsync(cancellationToken)).Select(row => new MissionTypeResponse
        {
            Id = row.Id, Code = row.Code, Name = row.Name, RobotType = row.RobotTypeCode, RobotTypeName = row.RobotTypeName,
            Mode = ToName(row.Mode), PickupKind = row.PickupKind is null ? null : ToName(row.PickupKind.Value),
            UsesSlotSensor = row.UsesSlotSensor, DepartDelaySeconds = row.DepartDelaySec,
            WaitTimeoutSeconds = row.WaitTimeoutSec, BasePriority = row.BasePriority
        }).ToList();

    private static string ToName(MissionMode value) => value == MissionMode.Batch ? "BATCH" : "SCHEDULE";
    private static string ToName(LocationKind value) => value switch
    {
        LocationKind.Pharmacy => "PHARMACY", LocationKind.Storage => "STORAGE", LocationKind.Specimen => "SPECIMEN",
        LocationKind.Dept => "DEPT", LocationKind.WasteStation => "WASTE_STATION", LocationKind.Charger => "CHARGER",
        LocationKind.CleanStation => "CLEAN_STATION", LocationKind.WastePoint => "WASTE_POINT", LocationKind.Corridor => "CORRIDOR",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
