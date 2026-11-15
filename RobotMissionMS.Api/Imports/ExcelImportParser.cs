using ClosedXML.Excel;
using System.Text.RegularExpressions;

namespace RobotMissionMS.Api.Imports;

public interface IExcelImportParser
{
    ExcelImportParseResult Parse(Stream content);
}

public sealed record ExcelImportParseResult(string TemplateVersion, IReadOnlyList<ExcelImportRow> Rows)
{
    public bool IsCurrentTemplate => TemplateVersion == ExcelImportParser.CurrentTemplateVersion;
}

public sealed record ExcelImportRow(int RowNo, string? RequestCode, string DepartmentCode, string ItemCode,
    string QuantityText, string? PriorityText, string? DeadlineText, string? Note);

public sealed class ExcelImportParser : IExcelImportParser
{
    public const string CurrentTemplateVersion = "RMMS-YC v3";
    public const int MaximumRows = 600;
    private static readonly string[] Headers = ["MaYeuCau", "Khoa", "MaHang", "SoLuong", "UuTien", "HanGiao", "GhiChu"];
    private static readonly Regex TemplateVersionPattern = new("^RMMS-YC v[0-9]+$", RegexOptions.CultureInvariant);

    public ExcelImportParseResult Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "YeuCau")
                ?? throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");
            if (workbook.Worksheets.All(x => x.Name != "DanhMuc")) throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");
            var metadata = workbook.Worksheets.FirstOrDefault(x => x.Name == "_RMMS")
                ?? throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");

            for (var column = 0; column < Headers.Length; column++)
                if (!string.Equals(sheet.Cell(4, column + 1).GetString().Trim(), Headers[column], StringComparison.Ordinal))
                    throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");

            if (!string.Equals(metadata.Cell(1, 1).GetString().Trim(), "templateVersion", StringComparison.Ordinal)
                || !string.Equals(metadata.Cell(2, 1).GetString().Trim(), "maxRows", StringComparison.Ordinal)
                || !int.TryParse(metadata.Cell(2, 2).GetString().Trim(), out var maximumRows)
                || maximumRows != MaximumRows)
                throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");

            var templateVersion = metadata.Cell(1, 2).GetString().Trim();
            if (templateVersion.Length > 30 || !TemplateVersionPattern.IsMatch(templateVersion))
                throw new InvalidOperationException("IMPORT_TEMPLATE_INVALID");

            var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.AllContents)?.RowNumber() ?? 4;
            var rows = new List<ExcelImportRow>();
            for (var rowNo = 5; rowNo <= lastRow; rowNo++)
            {
                var values = Enumerable.Range(1, Headers.Length).Select(column => sheet.Cell(rowNo, column).GetString().Trim()).ToArray();
                values[5] = GetDeadlineText(sheet.Cell(rowNo, 6));
                if (values.All(string.IsNullOrEmpty)) continue;
                rows.Add(new ExcelImportRow(rowNo, NullIfEmpty(values[0]), values[1], values[2], values[3],
                    NullIfEmpty(values[4]), NullIfEmpty(values[5]), NullIfEmpty(values[6])));
                if (rows.Count > MaximumRows) throw new InvalidOperationException("IMPORT_ROW_LIMIT_EXCEEDED");
            }

            return new ExcelImportParseResult(templateVersion, rows);
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("IMPORT_", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("IMPORT_FILE_INVALID", exception);
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static string GetDeadlineText(IXLCell cell)
    {
        if (cell.TryGetValue<TimeSpan>(out var time)) return time.ToString(@"hh\:mm");
        if (cell.TryGetValue<DateTime>(out var dateTime)) return dateTime.ToString("HH:mm");
        return cell.GetString().Trim();
    }
}
