using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
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
            .Select(value => new { ZoneId = value.ZoneId, FloorCode = value.Zone == null ? null : value.Zone.Code,
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
                MissionTypeCode = value.MissionType.Code, MissionTypeName = value.MissionType.Name, value.MissionTypeId,
                MissionTypeActive = value.MissionType.IsActive, IsBatch = value.MissionType.Mode == MissionMode.Batch,
                Pickup = value.MissionType.MissionTypePickupLocations
                    .Where(mapping => mapping.Zone.Code == user.FloorCode && mapping.Location.IsActive)
                    .Select(mapping => new { mapping.LocationId, mapping.Location.Code, mapping.Location.Name }).SingleOrDefault(),
                value.Packaging
            }).SingleOrDefaultAsync(cancellationToken);

        return new RequestPreviewData(user.ZoneId, user.FloorCode, user.FloorActive, department,
            item is null ? null : new RequestPreviewItem(item.Id, item.Code, item.Name, item.Unit, item.IsActive,
                item.MaxQtyPerSlot, item.KgPerUnit, item.MissionTypeCode, item.MissionTypeName, item.MissionTypeId,
                item.MissionTypeActive, item.IsBatch, item.Pickup?.LocationId, item.Pickup?.Code, item.Pickup?.Name, item.Packaging));
    }

    public async Task<RequestDetailData?> CreateManualRequestAsync(ManualRequestCreateData request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entity = new Request { Id = Guid.NewGuid(), RequestCode = request.RequestCode, ZoneId = request.ZoneId,
            Source = BatchSource.Manual, CreatedByUserId = request.CreatedByUserId, CreatedAt = request.CreatedAt };
        db.Requests.Add(entity);
        foreach (var mission in request.Missions)
        {
            db.Missions.Add(new Mission { Id = Guid.NewGuid(), MissionCode = mission.MissionCode,
                MissionTypeId = request.Item.MissionTypeId, ZoneId = request.ZoneId, RequestId = entity.Id,
                RequestCode = entity.RequestCode, FromLocationId = request.Item.PickupLocationId, ToLocationId = request.Department.Id,
                ItemId = request.Item.Id, Quantity = mission.Quantity, SlotNo = mission.SlotNo, Priority = MissionPriority.Normal,
                Status = MissionStatus.Pending, CreatedByUserId = request.CreatedByUserId, CreatedAt = request.CreatedAt,
                ItemCodeSnapshot = request.Item.Code, ItemNameSnapshot = request.Item.Name, UnitSnapshot = request.Item.Unit,
                PackagingSnapshot = request.Item.Packaging, KgPerUnitSnapshot = request.Item.KgPerUnit, WeightKgSnapshot = mission.WeightKg });
        }
        db.ActivityLogs.Add(new ActivityLog { UserId = request.CreatedByUserId, Timestamp = request.CreatedAt,
            Action = "MANUAL_REQUEST_CREATED", TargetObject = $"Request:{entity.Id:D};Code:{entity.RequestCode}" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await FindRequestByCodeAsync(entity.RequestCode, cancellationToken);
    }

    public async Task<RequestDetailData?> FindRequestByCodeAsync(string requestCode, CancellationToken cancellationToken = default)
    {
        var request = await db.Requests.AsNoTracking().Where(value => value.RequestCode == requestCode)
            .Select(value => new { value.Id, value.RequestCode, FloorCode = value.Zone.Code, FloorName = value.Zone.Name,
                value.Source, value.CreatedAt, CreatedByUsername = value.CreatedByUser.Username, value.Note })
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null) return null;
        var missions = await db.Missions.AsNoTracking().Where(value => value.RequestId == request.Id).OrderBy(value => value.SlotNo)
            .Select(value => new RequestMissionDetailData(value.MissionCode, value.Status, value.MissionType.Code,
                value.MissionType.Name, value.FromLocation!.Code, value.FromLocation.Name, value.ToLocation!.Code, value.ToLocation.Name,
                value.ItemCodeSnapshot!, value.ItemNameSnapshot!, value.UnitSnapshot!, value.PackagingSnapshot,
                value.KgPerUnitSnapshot!.Value, value.WeightKgSnapshot!.Value, value.Quantity!.Value, value.SlotNo!.Value, value.CreatedAt))
            .ToListAsync(cancellationToken);
        return new RequestDetailData(request.RequestCode, request.FloorCode, request.FloorName, request.Source, request.CreatedAt,
            request.CreatedByUsername, request.Note, missions);
    }
}
