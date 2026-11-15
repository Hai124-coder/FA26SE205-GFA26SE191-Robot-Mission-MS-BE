using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.Api.Imports;

public static class ImportResponseMapper
{
    public static ImportBatchResponse ToDetail(Batch batch) => new()
    {
        ImportId = batch.Id, BatchCode = batch.BatchCode, FileName = batch.FileName,
        Status = batch.Status.ToString().ToUpperInvariant(), TotalRows = batch.TotalRows,
        ValidRows = batch.AcceptedRows, InvalidRows = batch.RejectedRows, TasksToCreate = batch.GeneratedMissions,
        Rows = batch.BatchRows.OrderBy(row => row.RowNo).Select(ToRow).ToList()
    };

    public static ImportRowResponse ToRow(BatchRow row) => new()
    {
        RowNo = row.RowNo, RequestCode = row.RequestCode, Department = row.DepartmentCode, ItemCode = row.ItemCode,
        Quantity = row.Quantity, Priority = row.Priority?.ToString().ToUpperInvariant(), Deadline = row.DeadlineText,
        Note = row.Note, Warning = row.WarningMessage, Valid = row.IsValid, ErrorCode = row.ErrorCode, Error = row.ErrorMessage,
        Split = System.Text.Json.JsonSerializer.Deserialize<int[]>(row.SplitQuantities) ?? []
    };
}
