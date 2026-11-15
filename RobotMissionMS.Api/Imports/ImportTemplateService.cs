using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Api.Imports;

public interface IImportTemplateService
{
    Task<byte[]> CreateAsync(CancellationToken cancellationToken = default);
}

public sealed class ImportTemplateService(RobotMissionDbContext db) : IImportTemplateService
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
}
