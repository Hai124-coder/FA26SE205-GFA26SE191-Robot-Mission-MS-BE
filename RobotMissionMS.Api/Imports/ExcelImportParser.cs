using ClosedXML.Excel;

namespace RobotMissionMS.Api.Imports;

/// <summary>Reads only the stable RMMS-YC v3 input shape. Business validation belongs to the import service.</summary>
public interface IExcelImportParser
{
    IReadOnlyList<ExcelImportRow> Parse(Stream content);
}

public sealed record ExcelImportRow(int RowNo, string? RequestCode, string DepartmentCode, string ItemCode,
    string QuantityText, string? PriorityText, string? DeadlineText, string? Note);

public sealed class ExcelImportParser : IExcelImportParser
{
    private static readonly string[] Headers = ["MaYeuCau", "Khoa", "MaHang", "SoLuong", "UuTien", "HanGiao", "GhiChu"];
    public IReadOnlyList<ExcelImportRow> Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "YeuCau") ?? throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");
            for (var column = 0; column < Headers.Length; column++)
                if (!string.Equals(sheet.Cell(4, column + 1).GetString().Trim(), Headers[column], StringComparison.Ordinal))
                    throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");
            var rows = new List<ExcelImportRow>();
            for (var rowNo = 5; rowNo <= 604; rowNo++)
            {
                var values = Enumerable.Range(1, 7).Select(column => sheet.Cell(rowNo, column).GetString().Trim()).ToArray();
                if (values.All(string.IsNullOrEmpty)) continue;
                rows.Add(new ExcelImportRow(rowNo, NullIfEmpty(values[0]), values[1], values[2], values[3], NullIfEmpty(values[4]), NullIfEmpty(values[5]), NullIfEmpty(values[6])));
            }
            return rows;
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        { throw new InvalidOperationException("IMPORT_FILE_INVALID", exception); }
    }
    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
