using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

public sealed class RequestPreviewRepository(RobotMissionDbContext db) : IRequestPreviewRepository
{
    public async Task<RequestPreviewData?> GetPreviewDataAsync(Guid userId, string departmentCode, string itemCode,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().Where(value => value.Id == userId)
            .Select(value => new { FloorCode = value.Zone == null ? null : value.Zone.Code,
                FloorActive = value.Zone == null ? (bool?)null : value.Zone.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        var department = await db.Locations.AsNoTracking()
            .Where(value => value.Code == departmentCode && value.Kind == LocationKind.Dept)
            .Select(value => new RequestPreviewDepartment(value.Id, value.Code, value.Name, value.Zone.Code, value.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        var item = await db.Items.AsNoTracking().Where(value => value.Code == itemCode)
            .Select(value => new
            {
                value.Id, value.Code, value.Name, value.Unit, value.IsActive, value.MaxQtyPerSlot, value.KgPerUnit,
                MissionTypeCode = value.MissionType.Code, MissionTypeName = value.MissionType.Name,
                MissionTypeActive = value.MissionType.IsActive, IsBatch = value.MissionType.Mode == MissionMode.Batch,
                Pickup = value.MissionType.MissionTypePickupLocations
                    .Where(mapping => mapping.Zone.Code == user.FloorCode && mapping.Location.IsActive)
                    .Select(mapping => new { mapping.Location.Code, mapping.Location.Name }).SingleOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);

        return new RequestPreviewData(user.FloorCode, user.FloorActive, department,
            item is null ? null : new RequestPreviewItem(item.Id, item.Code, item.Name, item.Unit, item.IsActive,
                item.MaxQtyPerSlot, item.KgPerUnit, item.MissionTypeCode, item.MissionTypeName, item.MissionTypeActive,
                item.IsBatch, item.Pickup?.Code, item.Pickup?.Name));
    }
}
