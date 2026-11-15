using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Imports;

public interface IImportTemplateService
{
    Task<byte[]> CreateAsync(CancellationToken cancellationToken = default);
    Task<object> ImportAsync(Guid userId, string floor, IFormFile file, CancellationToken cancellationToken = default);
    Task<object?> GetAsync(Guid id, string? floor, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
    Task<object> CommitAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
    Task<object> ListAsync(string? floor, string? status, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<object> ListHospitalFilesAsync(bool? imported, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<object> ImportHospitalFileAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
}

public sealed class ImportTemplateService(RobotMissionDbContext db, IExcelImportParser parser) : IImportTemplateService
{
    public async Task<byte[]> CreateAsync(CancellationToken cancellationToken = default)
    {
        var items = await db.Items.AsNoTracking().Where(item => item.IsActive && item.MissionType.IsActive)
            .OrderBy(item => item.Code).Select(item => new { item.Code, item.Name, item.Unit, item.MaxQtyPerSlot,
                item.KgPerUnit, MissionType = item.MissionType.Code }).ToListAsync(cancellationToken);
        var departments = await db.Locations.AsNoTracking().Where(location => location.IsActive && location.Kind == LocationKind.Dept && location.Zone.IsActive)
            .OrderBy(location => location.Zone.Code).ThenBy(location => location.Code)
            .Select(location => new { Floor = location.Zone.Code, location.Code, location.Name }).ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var requests = workbook.Worksheets.Add("YeuCau");
        requests.Cell(1, 1).Value = "RMMS-YC v3";
        requests.Cell(2, 1).Value = "Nhập mỗi yêu cầu trên một dòng. Không sửa tên cột ở hàng 4.";
        var requestHeaders = new[] { "MaYeuCau", "Khoa", "MaHang", "SoLuong", "UuTien", "HanGiao", "GhiChu" };
        for (var column = 0; column < requestHeaders.Length; column++) requests.Cell(4, column + 1).Value = requestHeaders[column];
        requests.Range(4, 1, 4, requestHeaders.Length).Style.Font.Bold = true;
        requests.Range(4, 1, 4, requestHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("1F4E78");
        requests.Range(4, 1, 4, requestHeaders.Length).Style.Font.FontColor = XLColor.White;
        requests.Cell(5, 5).Value = "NORMAL";
        requests.Range("A4:G604").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        requests.SheetView.FreezeRows(4); requests.Columns().AdjustToContents();

        var catalogue = workbook.Worksheets.Add("DanhMuc");
        catalogue.Cell(1, 1).Value = "Danh mục được tạo từ cấu hình hiện tại; chỉ dùng để tra cứu.";
        var catalogueHeaders = new[] { "Loai", "Tang", "Ma", "Ten", "DonVi", "SucChuaMoiNgan", "KgMoiDonVi", "LoaiNhiemVu" };
        for (var column = 0; column < catalogueHeaders.Length; column++) catalogue.Cell(3, column + 1).Value = catalogueHeaders[column];
        var row = 4;
        foreach (var department in departments) { catalogue.Cell(row, 1).Value = "KHOA"; catalogue.Cell(row, 2).Value = department.Floor; catalogue.Cell(row, 3).Value = department.Code; catalogue.Cell(row, 4).Value = department.Name; row++; }
        foreach (var item in items) { catalogue.Cell(row, 1).Value = "HANG"; catalogue.Cell(row, 3).Value = item.Code; catalogue.Cell(row, 4).Value = item.Name; catalogue.Cell(row, 5).Value = item.Unit; catalogue.Cell(row, 6).Value = item.MaxQtyPerSlot; catalogue.Cell(row, 7).Value = item.KgPerUnit; catalogue.Cell(row, 8).Value = item.MissionType; row++; }
        catalogue.Range(3, 1, 3, catalogueHeaders.Length).Style.Font.Bold = true;
        catalogue.Range(3, 1, 3, catalogueHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("1F4E78");
        catalogue.Range(3, 1, 3, catalogueHeaders.Length).Style.Font.FontColor = XLColor.White;
        catalogue.SheetView.FreezeRows(3); catalogue.Columns().AdjustToContents();

        var metadata = workbook.Worksheets.Add("_RMMS");
        metadata.Cell(1, 1).Value = "templateVersion"; metadata.Cell(1, 2).Value = "RMMS-YC v3";
        metadata.Cell(2, 1).Value = "maxRows"; metadata.Cell(2, 2).Value = 600;
        metadata.Visibility = XLWorksheetVisibility.VeryHidden;
        await using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public async Task<object> ImportAsync(Guid userId, string floor, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("IMPORT_FILE_INVALID");
        await using var input = file.OpenReadStream(); var inputRows = parser.Parse(input);
        var items = await db.Items.Include(x => x.MissionType).ThenInclude(x => x.MissionTypePickupLocations).ThenInclude(x => x.Location)
            .Where(x => x.IsActive && x.MissionType.IsActive && x.MissionType.Mode == MissionMode.Batch).ToDictionaryAsync(x => x.Code, cancellationToken);
        var departments = await db.Locations.Include(x => x.Zone).Where(x => x.IsActive && x.Kind == LocationKind.Dept).ToDictionaryAsync(x => x.Code, cancellationToken);
        var now = DateTime.UtcNow; var batch = new RobotMissionMS.DataAccess.Entities.Batch { Id = Guid.NewGuid(), BatchCode = $"IMP-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), FileName = Path.GetFileName(file.FileName), CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now, TemplateVersion = "RMMS-YC v3", Source = BatchSource.Import, Status = BatchStatus.Draft };
        var rows = new List<RobotMissionMS.DataAccess.Entities.BatchRow>();
        foreach (var inputRow in inputRows) { var rowNo = inputRow.RowNo; var departmentCode = inputRow.DepartmentCode; var itemCode = inputRow.ItemCode; var quantityText = inputRow.QuantityText; RobotMissionMS.DataAccess.Entities.Location? department = null; RobotMissionMS.DataAccess.Entities.Item? item = null;
            var valid = int.TryParse(quantityText, out var quantity) && quantity is >= 1 and <= 600 && departments.TryGetValue(departmentCode, out department) && department.Zone.Code == floor && items.TryGetValue(itemCode, out item) && item.KgPerUnit is > 0 && item.MaxQtyPerSlot > 0 && item.MissionType.MissionTypePickupLocations.Any(x => x.ZoneId == department.ZoneId && x.Location.IsActive);
            var splits = valid ? Enumerable.Range(0, (quantity + item!.MaxQtyPerSlot - 1) / item.MaxQtyPerSlot).Select(i => Math.Min(item.MaxQtyPerSlot, quantity - i * item.MaxQtyPerSlot)).ToArray() : [];
            rows.Add(new RobotMissionMS.DataAccess.Entities.BatchRow { Id = Guid.NewGuid(), BatchId = batch.Id, RowNo = rowNo, RequestCode = inputRow.RequestCode, DepartmentCode = departmentCode, ItemCode = itemCode, Quantity = int.TryParse(quantityText, out var q) ? q : null, Priority = MissionPriority.Normal, DeadlineText = inputRow.DeadlineText, Note = inputRow.Note, ZoneId = valid ? department!.ZoneId : null, DepartmentLocationId = valid ? department!.Id : null, ItemId = valid ? item!.Id : null, MissionTypeId = valid ? item!.MissionTypeId : null, IsValid = valid, ErrorCode = valid ? null : "C3", ErrorMessage = valid ? null : "Dòng không hợp lệ hoặc không thuộc tầng Operator.", SplitQuantities = System.Text.Json.JsonSerializer.Serialize(splits), MissionsToCreate = splits.Length, CreatedAt = now, UpdatedAt = now }); }
        batch.TotalRows = rows.Count; batch.AcceptedRows = rows.Count(x => x.IsValid); batch.RejectedRows = batch.TotalRows - batch.AcceptedRows; batch.GeneratedMissions = rows.Sum(x => x.MissionsToCreate); db.Batches.Add(batch); db.BatchRows.AddRange(rows); await db.SaveChangesAsync(cancellationToken); return await GetAsync(batch.Id, floor, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<object?> GetAsync(Guid id, string? floor, CancellationToken cancellationToken = default)
    {
        var batch = await db.Batches.AsNoTracking().Include(x => x.BatchRows).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); if (batch is null) return null;
        if (floor is not null && batch.BatchRows.Any(x => x.ZoneId != null && x.ZoneId != db.Zones.Where(z => z.Code == floor).Select(z => z.Id).FirstOrDefault())) return null;
        return new { importId = batch.Id, batch.BatchCode, batch.FileName, status = batch.Status.ToString().ToUpperInvariant(), batch.TemplateVersion, totalRows = batch.TotalRows, validRows = batch.AcceptedRows, invalidRows = batch.RejectedRows, tasksToCreate = batch.GeneratedMissions, rows = batch.BatchRows.OrderBy(x => x.RowNo).Select(x => new { x.RowNo, x.RequestCode, department = x.DepartmentCode, x.ItemCode, x.Quantity, valid = x.IsValid, x.ErrorCode, error = x.ErrorMessage, split = x.SplitQuantities }) };
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    { var batch = await db.Batches.Include(x => x.BatchRows).SingleOrDefaultAsync(x => x.Id == id && x.Status == BatchStatus.Draft, cancellationToken); if (batch is null || batch.CreatedByUserId != userId || batch.BatchRows.Any(x => x.ZoneId != null && x.ZoneId != db.Zones.Where(z => z.Code == floor).Select(z => z.Id).FirstOrDefault())) return false; db.Batches.Remove(batch); await db.SaveChangesAsync(cancellationToken); return true; }

    public async Task<object> CommitAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.Batches.Include(x => x.BatchRows).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("IMPORT_NOT_FOUND");
        if (batch.Status != BatchStatus.Draft) throw new InvalidOperationException("IMPORT_ALREADY_COMMITTED");
        if (batch.CreatedByUserId != userId) throw new InvalidOperationException("IMPORT_FORBIDDEN");
        var zoneId = await db.Zones.Where(x => x.Code == floor && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("FLOORS_INACTIVE");
        var validRows = batch.BatchRows.Where(x => x.IsValid).OrderBy(x => x.RowNo).ToList();
        foreach (var row in validRows)
        {
            var item = await db.Items.Include(x => x.MissionType).ThenInclude(x => x.MissionTypePickupLocations).ThenInclude(x => x.Location)
                .SingleOrDefaultAsync(x => x.Id == row.ItemId && x.IsActive && x.MissionType.IsActive && x.MissionType.Mode == MissionMode.Batch, cancellationToken);
            var department = await db.Locations.SingleOrDefaultAsync(x => x.Id == row.DepartmentLocationId && x.IsActive && x.ZoneId == zoneId && x.Kind == LocationKind.Dept, cancellationToken);
            if (item is null || department is null || item.KgPerUnit is not > 0 || item.MaxQtyPerSlot <= 0) throw new InvalidOperationException("IMPORT_STALE");
            var pickup = item.MissionType.MissionTypePickupLocations.SingleOrDefault(x => x.ZoneId == zoneId && x.Location.IsActive)?.Location;
            if (pickup is null) throw new InvalidOperationException("PICKUP_LOCATION_NOT_CONFIGURED");
            var requestCode = string.IsNullOrWhiteSpace(row.RequestCode) ? $"YC-{Guid.NewGuid():N}"[..11].ToUpperInvariant() : row.RequestCode;
            if (await db.Requests.AnyAsync(x => x.RequestCode == requestCode, cancellationToken)) throw new InvalidOperationException("REQUEST_CODE_DUPLICATE");
            var request = new RobotMissionMS.DataAccess.Entities.Request { Id = Guid.NewGuid(), RequestCode = requestCode, ZoneId = zoneId, BatchId = batch.Id, Source = BatchSource.Import, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow, Note = row.Note };
            db.Requests.Add(request); var remaining = row.Quantity!.Value; var slot = 0;
            while (remaining > 0) { var quantity = Math.Min(remaining, item.MaxQtyPerSlot); slot++; db.Missions.Add(new RobotMissionMS.DataAccess.Entities.Mission { Id = Guid.NewGuid(), MissionCode = $"NV-{requestCode[3..]}-{slot:D2}", MissionTypeId = item.MissionTypeId, ZoneId = zoneId, BatchId = batch.Id, RequestId = request.Id, RequestCode = requestCode, FromLocationId = pickup.Id, ToLocationId = department.Id, ItemId = item.Id, Quantity = quantity, SlotNo = slot, Priority = row.Priority ?? MissionPriority.Normal, Status = MissionStatus.Pending, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow, ItemCodeSnapshot = item.Code, ItemNameSnapshot = item.Name, UnitSnapshot = item.Unit, PackagingSnapshot = item.Packaging, KgPerUnitSnapshot = item.KgPerUnit, WeightKgSnapshot = quantity * item.KgPerUnit.Value, Note = row.Note }); remaining -= quantity; }
        }
        batch.Status = BatchStatus.Committed; batch.CommittedAt = DateTime.UtcNow; batch.CommittedByUserId = userId; batch.UpdatedAt = DateTime.UtcNow; batch.GeneratedMissions = validRows.Sum(x => x.MissionsToCreate);
        db.ActivityLogs.Add(new RobotMissionMS.DataAccess.Entities.ActivityLog { UserId = userId, Timestamp = DateTime.UtcNow, Action = "IMPORT_COMMITTED", TargetObject = $"Batch:{batch.Id:D}" });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new { importId = batch.Id, status = "COMMITTED", acceptedRows = validRows.Count, rejectedRows = batch.RejectedRows, generatedMissions = batch.GeneratedMissions };
    }
    public async Task<object> ListAsync(string? floor, string? status, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    { var query = db.Batches.AsNoTracking().AsQueryable(); if (!string.IsNullOrWhiteSpace(floor)) query = query.Where(x => x.BatchRows.Any(row => row.Zone != null && row.Zone.Code == floor)); if (from.HasValue) query = query.Where(x => x.CreatedAt >= from); if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.AddDays(1)); if (Enum.TryParse<BatchStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed); var rows = await query.OrderByDescending(x => x.CreatedAt).Select(x => new { importId = x.Id, x.BatchCode, x.FileName, status = x.Status, x.TemplateVersion, totalRows = x.TotalRows, validRows = x.AcceptedRows, invalidRows = x.RejectedRows, tasksToCreate = x.GeneratedMissions, x.CreatedAt }).ToListAsync(cancellationToken); return rows.Select(x => new { x.importId, x.BatchCode, x.FileName, status = x.status.ToString().ToUpperInvariant(), x.TemplateVersion, x.totalRows, x.validRows, x.invalidRows, x.tasksToCreate, x.CreatedAt }); }
    public async Task<object> ListHospitalFilesAsync(bool? imported, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    { var query = db.HospitalFiles.AsNoTracking().AsQueryable(); if (imported.HasValue) query = query.Where(x => (x.ImportedBatchId != null) == imported.Value); if (from.HasValue) query = query.Where(x => x.ReceivedAt >= from); if (to.HasValue) query = query.Where(x => x.ReceivedAt < to.Value.AddDays(1)); return await query.OrderByDescending(x => x.ReceivedAt).Select(x => new { id = x.Id, x.FileName, x.ContentType, x.SizeBytes, x.SenderName, x.ReceivedAt, x.Note, isImported = x.ImportedBatchId != null, x.ImportedAt, importedBy = x.ImportedByUser == null ? null : x.ImportedByUser.FullName, x.ImportedBatchId, batchCode = x.ImportedBatch == null ? null : x.ImportedBatch.BatchCode }).ToListAsync(cancellationToken); }
    public async Task<object> ImportHospitalFileAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    { var file = await db.HospitalFiles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("HOSPITAL_FILE_NOT_FOUND"); if (file.ImportedBatchId != null) throw new InvalidOperationException("HOSPITAL_FILE_ALREADY_IMPORTED"); using var stream = new MemoryStream(file.Content, writable: false); var upload = new Microsoft.AspNetCore.Http.FormFile(stream, 0, file.Content.Length, "file", file.FileName) { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = file.ContentType }; await ImportAsync(userId, floor, upload, cancellationToken); var batch = await db.Batches.OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.CreatedByUserId == userId && x.FileName == file.FileName, cancellationToken); file.ImportedBatchId = batch.Id; file.ImportedAt = DateTime.UtcNow; file.ImportedByUserId = userId; await db.SaveChangesAsync(cancellationToken); return await GetAsync(batch.Id, floor, cancellationToken) ?? throw new InvalidOperationException(); }
}
