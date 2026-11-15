using ClosedXML.Excel;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Imports;

public interface IImportTemplateService
{
    Task<byte[]> CreateAsync(CancellationToken cancellationToken = default);
    Task<ImportBatchResponse> ImportAsync(Guid userId, string floor, IFormFile file, CancellationToken cancellationToken = default);
    Task<ImportBatchResponse?> GetAsync(Guid id, string? floor, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
    Task<ImportCommitResponse> CommitAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportListItemResponse>> ListAsync(string? floor, string? status, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HospitalFileResponse>> ListHospitalFilesAsync(bool? imported, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<ImportBatchResponse> ImportHospitalFileAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default);
}

public sealed class ImportTemplateService(RobotMissionDbContext db, IExcelImportParser parser) : IImportTemplateService
{
    private static readonly Regex RequestCodePattern = new("^YC-[A-Za-z0-9-]{1,46}$", RegexOptions.CultureInvariant);

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
        requests.SheetView.FreezeRows(4);

        var catalogue = workbook.Worksheets.Add("DanhMuc");
        catalogue.Cell(1, 1).Value = "Danh mục được tạo từ cấu hình hiện tại; chỉ dùng để tra cứu.";
        var catalogueHeaders = new[] { "Loai", "Tang", "Ma", "Ten", "DonVi", "SucChuaMoiNgan", "KgMoiDonVi", "LoaiNhiemVu" };
        for (var column = 0; column < catalogueHeaders.Length; column++) catalogue.Cell(3, column + 1).Value = catalogueHeaders[column];
        var row = 4;
        var departmentStartRow = row;
        foreach (var department in departments) { catalogue.Cell(row, 1).Value = "KHOA"; catalogue.Cell(row, 2).Value = department.Floor; catalogue.Cell(row, 3).Value = department.Code; catalogue.Cell(row, 4).Value = department.Name; row++; }
        var departmentEndRow = row - 1;
        var itemStartRow = row;
        foreach (var item in items) { catalogue.Cell(row, 1).Value = "HANG"; catalogue.Cell(row, 3).Value = item.Code; catalogue.Cell(row, 4).Value = item.Name; catalogue.Cell(row, 5).Value = item.Unit; catalogue.Cell(row, 6).Value = item.MaxQtyPerSlot; catalogue.Cell(row, 7).Value = item.KgPerUnit; catalogue.Cell(row, 8).Value = item.MissionType; row++; }
        var itemEndRow = row - 1;
        catalogue.Cell(3, 10).Value = "UuTien";
        catalogue.Cell(4, 10).Value = "NORMAL"; catalogue.Cell(5, 10).Value = "HIGH"; catalogue.Cell(6, 10).Value = "URGENT";
        catalogue.Range(3, 1, 3, catalogueHeaders.Length).Style.Font.Bold = true;
        catalogue.Range(3, 1, 3, catalogueHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("1F4E78");
        catalogue.Range(3, 1, 3, catalogueHeaders.Length).Style.Font.FontColor = XLColor.White;
        catalogue.SheetView.FreezeRows(3); catalogue.Columns().AdjustToContents();

        if (departmentEndRow >= departmentStartRow)
            requests.Range(5, 2, 604, 2).CreateDataValidation().List(catalogue.Range(departmentStartRow, 3, departmentEndRow, 3));
        if (itemEndRow >= itemStartRow)
            requests.Range(5, 3, 604, 3).CreateDataValidation().List(catalogue.Range(itemStartRow, 3, itemEndRow, 3));
        requests.Range(5, 5, 604, 5).CreateDataValidation().List(catalogue.Range(4, 10, 6, 10));
        requests.Range("A5:G604").Style.Protection.Locked = false;
        requests.Protect();
        requests.Columns().AdjustToContents();

        var metadata = workbook.Worksheets.Add("_RMMS");
        metadata.Cell(1, 1).Value = "templateVersion"; metadata.Cell(1, 2).Value = ExcelImportParser.CurrentTemplateVersion;
        metadata.Cell(2, 1).Value = "maxRows"; metadata.Cell(2, 2).Value = ExcelImportParser.MaximumRows;
        metadata.Visibility = XLWorksheetVisibility.VeryHidden;
        await using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public async Task<ImportBatchResponse> ImportAsync(Guid userId, string floor, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0 || file.FileName.Length > 255 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IMPORT_FILE_INVALID");
        await using var input = file.OpenReadStream(); return (await CreateDraftAsync(input, file.FileName, userId, floor, cancellationToken)).Response;
    }

    private async Task<ImportDraftResult> CreateDraftAsync(Stream input, string fileName, Guid userId, string floor, CancellationToken cancellationToken)
    {
        var parsedImport = parser.Parse(input);
        var inputRows = parsedImport.Rows;
        var items = await db.Items.Include(x => x.MissionType).ThenInclude(x => x.MissionTypePickupLocations).ThenInclude(x => x.Location)
            .Where(x => x.IsActive && x.MissionType.IsActive && x.MissionType.Mode == MissionMode.Batch).ToDictionaryAsync(x => x.Code, cancellationToken);
        var departments = await db.Locations.Include(x => x.Zone).Where(x => x.IsActive && x.Kind == LocationKind.Dept).ToDictionaryAsync(x => x.Code, cancellationToken);
        var zoneId = await db.Zones.Where(x => x.Code == floor && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("FLOORS_INACTIVE");
        var now = DateTime.UtcNow; var batch = new RobotMissionMS.DataAccess.Entities.Batch { Id = Guid.NewGuid(), ZoneId = zoneId, BatchCode = $"IMP-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), FileName = Path.GetFileName(fileName), CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now, TemplateVersion = parsedImport.TemplateVersion, Source = BatchSource.Import, Status = BatchStatus.Draft };
        var rows = new List<RobotMissionMS.DataAccess.Entities.BatchRow>();
        foreach (var inputRow in inputRows) { var rowNo = inputRow.RowNo; var departmentCode = inputRow.DepartmentCode; var itemCode = inputRow.ItemCode; var quantityText = inputRow.QuantityText; RobotMissionMS.DataAccess.Entities.Location? department = null; RobotMissionMS.DataAccess.Entities.Item? item = null;
            var quantity = 0; var priorityValid = TryParsePriority(inputRow.PriorityText, out var priority);
            var deadlineValid = ImportDeadline.TryGetUtc(inputRow.DeadlineText, out var deadlineAt);
            var fieldsValid = IsValidRequestCode(inputRow.RequestCode) && departmentCode.Length is > 0 and <= 50 && itemCode.Length is > 0 and <= 50 && inputRow.Note?.Length <= 1000 && priorityValid && deadlineValid && int.TryParse(quantityText, out quantity) && quantity is >= 1 and <= 600;
            var departmentFound = departments.TryGetValue(departmentCode, out department);
            var floorAllowed = departmentFound && department!.Zone.Code == floor;
            var itemFound = items.TryGetValue(itemCode, out item);
            var itemConfigured = itemFound && departmentFound && item!.KgPerUnit is > 0 && item.MaxQtyPerSlot > 0
                && item.MissionType.MissionTypePickupLocations.Any(x => x.ZoneId == department!.ZoneId && x.Location.IsActive);
            var pickup = itemConfigured
                ? item!.MissionType.MissionTypePickupLocations.First(x => x.ZoneId == department!.ZoneId && x.Location.IsActive).Location
                : null;
            var valid = fieldsValid && floorAllowed && itemConfigured;
            var splits = valid ? Enumerable.Range(0, (quantity + item!.MaxQtyPerSlot - 1) / item.MaxQtyPerSlot).Select(i => Math.Min(item.MaxQtyPerSlot, quantity - i * item.MaxQtyPerSlot)).ToArray() : [];
            var (errorCode, errorMessage) = GetRowError(fieldsValid, departmentFound, floorAllowed, itemFound, itemConfigured, inputRow);
            var snapshot = valid ? new ImportRowSnapshot(splits, item!.MaxQtyPerSlot, item.KgPerUnit!.Value, pickup!.Id, deadlineAt) : null;
            rows.Add(new RobotMissionMS.DataAccess.Entities.BatchRow { Id = Guid.NewGuid(), BatchId = batch.Id, RowNo = rowNo, RequestCode = inputRow.RequestCode, DepartmentCode = departmentCode, ItemCode = itemCode, Quantity = int.TryParse(quantityText, out var q) ? q : null, Priority = priorityValid ? priority : null, DeadlineText = inputRow.DeadlineText, Note = inputRow.Note, ZoneId = valid ? department!.ZoneId : null, DepartmentLocationId = valid ? department!.Id : null, ItemId = valid ? item!.Id : null, MissionTypeId = valid ? item!.MissionTypeId : null, IsValid = valid, ErrorCode = valid ? null : errorCode, ErrorMessage = valid ? null : errorMessage, WarningMessage = parsedImport.IsCurrentTemplate ? null : "Mẫu Excel cũ; hãy tải mẫu RMMS-YC v3 mới trước lần import tiếp theo.", SplitQuantities = snapshot?.ToJson() ?? System.Text.Json.JsonSerializer.Serialize(splits), MissionsToCreate = splits.Length, CreatedAt = now, UpdatedAt = now }); }
        batch.TotalRows = rows.Count; batch.AcceptedRows = rows.Count(x => x.IsValid); batch.RejectedRows = batch.TotalRows - batch.AcceptedRows; batch.GeneratedMissions = rows.Sum(x => x.MissionsToCreate); db.Batches.Add(batch); db.BatchRows.AddRange(rows); await db.SaveChangesAsync(cancellationToken); return new ImportDraftResult(batch.Id, ImportResponseMapper.ToDetail(batch));
    }

    public async Task<ImportBatchResponse?> GetAsync(Guid id, string? floor, CancellationToken cancellationToken = default)
    {
        var batch = await db.Batches.AsNoTracking().Include(x => x.Zone).Include(x => x.BatchRows).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); if (batch is null) return null;
        if (floor is not null && batch.Zone?.Code != floor) return null;
        return ImportResponseMapper.ToDetail(batch);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    { var batch = await db.Batches.Include(x => x.BatchRows).SingleOrDefaultAsync(x => x.Id == id && x.Status == BatchStatus.Draft, cancellationToken); if (batch is null || batch.CreatedByUserId != userId || batch.BatchRows.Any(x => x.ZoneId != null && x.ZoneId != db.Zones.Where(z => z.Code == floor).Select(z => z.Id).FirstOrDefault())) return false; db.Batches.Remove(batch); await db.SaveChangesAsync(cancellationToken); return true; }

    public async Task<ImportCommitResponse> CommitAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockedBatch = await db.Batches
                .FromSqlInterpolated($"""SELECT * FROM public."Batches" WHERE "Id" = {id} FOR UPDATE""")
                .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("IMPORT_NOT_FOUND");
            var batch = await db.Batches.Include(x => x.BatchRows).SingleAsync(x => x.Id == lockedBatch.Id, cancellationToken);
            if (batch.Status != BatchStatus.Draft) throw new InvalidOperationException("IMPORT_ALREADY_COMMITTED");
            if (batch.CreatedByUserId != userId) throw new InvalidOperationException("IMPORT_FORBIDDEN");
            var zoneId = await db.Zones.Where(x => x.Code == floor && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("FLOORS_INACTIVE");
            var validRows = batch.BatchRows.Where(x => x.IsValid).OrderBy(x => x.RowNo).ToList();
            var suppliedCodes = validRows.Where(row => !string.IsNullOrWhiteSpace(row.RequestCode)).Select(row => row.RequestCode!.Trim());
            if (suppliedCodes.GroupBy(code => code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new InvalidOperationException("REQUEST_CODE_DUPLICATE");

            var generatedMissions = 0;
            foreach (var row in validRows)
            {
                if (!ImportRowSnapshot.TryRead(row.SplitQuantities, out var snapshot)
                    || snapshot!.Quantities is null
                    || snapshot.Quantities.Count == 0
                    || snapshot.Quantities.Any(quantity => quantity <= 0 || quantity > snapshot.MaxQtyPerSlot)
                    || snapshot.Quantities.Sum() != row.Quantity
                    || snapshot.Quantities.Count != row.MissionsToCreate
                    || !IsValidRequestCode(row.RequestCode)
                    || row.Priority is null)
                    throw new InvalidOperationException("IMPORT_STALE");
                var item = await db.Items.Include(x => x.MissionType).ThenInclude(x => x.MissionTypePickupLocations).ThenInclude(x => x.Location)
                    .SingleOrDefaultAsync(x => x.Id == row.ItemId && x.IsActive && x.MissionType.IsActive && x.MissionType.Mode == MissionMode.Batch, cancellationToken);
                var department = await db.Locations.SingleOrDefaultAsync(x => x.Id == row.DepartmentLocationId && x.IsActive && x.ZoneId == zoneId && x.Kind == LocationKind.Dept, cancellationToken);
                if (item is null || department is null || item.KgPerUnit is not > 0 || item.MaxQtyPerSlot <= 0) throw new InvalidOperationException("IMPORT_STALE");
                var pickup = item.MissionType.MissionTypePickupLocations.FirstOrDefault(x => x.ZoneId == zoneId && x.Location.IsActive)?.Location;
                if (pickup is null || pickup.Id != snapshot.PickupLocationId || item.MaxQtyPerSlot != snapshot.MaxQtyPerSlot || item.KgPerUnit.Value != snapshot.KgPerUnit)
                    throw new InvalidOperationException("IMPORT_STALE");
                var requestCode = string.IsNullOrWhiteSpace(row.RequestCode) ? $"YC-{Guid.NewGuid():N}"[..11].ToUpperInvariant() : row.RequestCode.Trim();
                if (await db.Requests.AnyAsync(x => x.RequestCode == requestCode, cancellationToken)) throw new InvalidOperationException("REQUEST_CODE_DUPLICATE");
                var createdAt = DateTime.UtcNow;
                var request = new RobotMissionMS.DataAccess.Entities.Request { Id = Guid.NewGuid(), RequestCode = requestCode, ZoneId = zoneId, BatchId = batch.Id, Source = BatchSource.Import, CreatedByUserId = userId, CreatedAt = createdAt, Note = row.Note };
                db.Requests.Add(request); var slot = 0;
                foreach (var quantity in snapshot.Quantities) { slot++; generatedMissions++; db.Missions.Add(new RobotMissionMS.DataAccess.Entities.Mission { Id = Guid.NewGuid(), MissionCode = $"NV-{requestCode[3..]}-{slot:D2}", MissionTypeId = item.MissionTypeId, ZoneId = zoneId, BatchId = batch.Id, RequestId = request.Id, RequestCode = requestCode, FromLocationId = pickup.Id, ToLocationId = department.Id, ItemId = item.Id, Quantity = quantity, SlotNo = slot, Priority = row.Priority.Value, Status = MissionStatus.Pending, CreatedByUserId = userId, CreatedAt = createdAt, ItemCodeSnapshot = item.Code, ItemNameSnapshot = item.Name, UnitSnapshot = item.Unit, PackagingSnapshot = item.Packaging, KgPerUnitSnapshot = snapshot.KgPerUnit, WeightKgSnapshot = quantity * snapshot.KgPerUnit, DeadlineAt = snapshot.DeadlineAt, Note = row.Note }); }
            }
            batch.Status = BatchStatus.Committed; batch.CommittedAt = DateTime.UtcNow; batch.CommittedByUserId = userId; batch.UpdatedAt = DateTime.UtcNow; batch.GeneratedMissions = generatedMissions;
            db.ActivityLogs.Add(new RobotMissionMS.DataAccess.Entities.ActivityLog { UserId = userId, Timestamp = DateTime.UtcNow, Action = "IMPORT_COMMITTED", TargetObject = $"Batch:{batch.Id:D}" });
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return new ImportCommitResponse { ImportId = batch.Id, Status = "COMMITTED", AcceptedRows = validRows.Count, RejectedRows = batch.RejectedRows, GeneratedMissions = batch.GeneratedMissions, CommittedAt = new DateTimeOffset(batch.CommittedAt!.Value) };
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "Requests_RequestCode_key" })
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException("REQUEST_CODE_DUPLICATE", exception);
        }
    }
    public async Task<IReadOnlyList<ImportListItemResponse>> ListAsync(string? floor, string? status, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    { var query = db.Batches.AsNoTracking().AsQueryable(); if (!string.IsNullOrWhiteSpace(floor)) query = query.Where(x => x.Zone != null && x.Zone.Code == floor); if (from.HasValue) query = query.Where(x => x.CreatedAt >= from); if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.AddDays(1)); if (Enum.TryParse<BatchStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed); return await query.OrderByDescending(x => x.CreatedAt).Select(x => new ImportListItemResponse { ImportId = x.Id, BatchCode = x.BatchCode, FileName = x.FileName, Status = x.Status.ToString().ToUpperInvariant(), TotalRows = x.TotalRows, ValidRows = x.AcceptedRows, InvalidRows = x.RejectedRows, TasksToCreate = x.GeneratedMissions, CreatedAt = new DateTimeOffset(x.CreatedAt) }).ToListAsync(cancellationToken); }
    public async Task<IReadOnlyList<HospitalFileResponse>> ListHospitalFilesAsync(bool? imported, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    { var query = db.HospitalFiles.AsNoTracking().AsQueryable(); if (imported.HasValue) query = query.Where(x => (x.ImportedBatchId != null) == imported.Value); if (from.HasValue) query = query.Where(x => x.ReceivedAt >= from); if (to.HasValue) query = query.Where(x => x.ReceivedAt < to.Value.AddDays(1)); return await query.OrderByDescending(x => x.ReceivedAt).Select(x => new HospitalFileResponse { Id = x.Id, FileName = x.FileName, ContentType = x.ContentType, SizeBytes = x.SizeBytes, SenderName = x.SenderName, ReceivedAt = new DateTimeOffset(x.ReceivedAt), Note = x.Note, IsImported = x.ImportedBatchId != null, ImportedAt = x.ImportedAt == null ? null : new DateTimeOffset(x.ImportedAt.Value), ImportedBy = x.ImportedByUser == null ? null : x.ImportedByUser.FullName, ImportedBatchId = x.ImportedBatchId, BatchCode = x.ImportedBatch == null ? null : x.ImportedBatch.BatchCode }).ToListAsync(cancellationToken); }
    public async Task<ImportBatchResponse> ImportHospitalFileAsync(Guid id, Guid userId, string floor, CancellationToken cancellationToken = default)
    { await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var file = await db.HospitalFiles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("HOSPITAL_FILE_NOT_FOUND"); if (file.ImportedBatchId != null) throw new InvalidOperationException("HOSPITAL_FILE_ALREADY_IMPORTED"); using var stream = new MemoryStream(file.Content, writable: false); var draft = await CreateDraftAsync(stream, file.FileName, userId, floor, cancellationToken); file.ImportedBatchId = draft.BatchId; file.ImportedAt = DateTime.UtcNow; file.ImportedByUserId = userId; await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return draft.Response; }

    private static bool IsValidRequestCode(string? value) => string.IsNullOrWhiteSpace(value) || RequestCodePattern.IsMatch(value);

    private static bool TryParsePriority(string? value, out MissionPriority priority)
    {
        priority = MissionPriority.Normal;
        return value?.Trim().ToUpperInvariant() switch
        {
            "NORMAL" or "THƯỜNG" or "THUONG" => true,
            "HIGH" or "CAO" => SetPriority(MissionPriority.High, out priority),
            "URGENT" or "KHẨN" or "KHAN" => SetPriority(MissionPriority.Urgent, out priority),
            _ => false
        };
    }

    private static bool SetPriority(MissionPriority value, out MissionPriority priority) { priority = value; return true; }

    private static (string Code, string Message) GetRowError(bool fieldsValid, bool departmentFound, bool floorAllowed,
        bool itemFound, bool itemConfigured, ExcelImportRow row)
    {
        if (!fieldsValid) return ("C1", "Dữ liệu dòng không hợp lệ: mã yêu cầu, khoa, mã hàng, số lượng, ưu tiên, hạn giao hoặc ghi chú.");
        if (!departmentFound) return ("C1", "Khoa nhận không tồn tại hoặc không còn hoạt động.");
        if (!floorAllowed) return ("FLOOR_FORBIDDEN", "Operator tầng hiện tại không được nhập dòng của khoa này.");
        if (!itemFound) return ("C3", "Mã hàng không có trong danh mục hàng robot chở.");
        if (!itemConfigured) return ("C3", "Mặt hàng chưa có cấu hình sức chứa, khối lượng hoặc điểm lấy phù hợp.");
        throw new InvalidOperationException("A valid import row cannot have an error.");
    }
}
