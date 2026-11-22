using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Reports.DTOs;
using RobotMissionMS.Business.Features.Reports.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = AuthPolicies.SupervisorOnly)]
public sealed class ReportsController(IOperationsReportService reports) : ControllerBase
{
    [HttpGet("operations")]
    [ProducesResponseType(typeof(OperationsReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationsReportResponse>> Operations([FromQuery] OperationsReportQuery query, CancellationToken cancellationToken)
    {
        var result = await reports.GetAsync(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    [HttpGet("operations/export")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportOperations([FromQuery] OperationsReportQuery query, [FromQuery] string format, CancellationToken cancellationToken)
    {
        var result = await reports.GetAsync(query, cancellationToken);
        if (!result.IsSuccess) return Failure(result.Error!.Value);
        var report = result.Response!;
        return format?.Trim().ToLowerInvariant() switch
        {
            "csv" => File(Csv(report), "text/csv; charset=utf-8", "operations-report.csv"),
            "xlsx" => File(Xlsx(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "operations-report.xlsx"),
            _ => Problem(type: "OPERATIONS_REPORT_FORMAT_INVALID", statusCode: StatusCodes.Status400BadRequest)
        };
    }

    private static ActionResult Failure(OperationsReportError error) => error switch
    {
        OperationsReportError.InvalidQuery => new ObjectResult(new ProblemDetails { Type = "OPERATIONS_REPORT_INVALID_QUERY", Status = StatusCodes.Status400BadRequest }) { StatusCode = StatusCodes.Status400BadRequest },
        OperationsReportError.RangeTooLarge => new ObjectResult(new ProblemDetails { Type = "OPERATIONS_REPORT_RANGE_TOO_LARGE", Status = StatusCodes.Status400BadRequest }) { StatusCode = StatusCodes.Status400BadRequest },
        _ => throw new InvalidOperationException("Operations report service returned an invalid result.")
    };

    private static byte[] Csv(OperationsReportResponse report)
    {
        var builder = new StringBuilder("bucket_start,metric,key,value\n");
        foreach (var bucket in report.Buckets)
        {
            Add(builder, bucket.StartAt, "outcome", "PENDING", bucket.Outcomes.Pending); Add(builder, bucket.StartAt, "outcome", "ASSIGNED", bucket.Outcomes.Assigned);
            Add(builder, bucket.StartAt, "outcome", "IN_PROGRESS", bucket.Outcomes.InProgress); Add(builder, bucket.StartAt, "outcome", "DELIVERY_FAILED", bucket.Outcomes.DeliveryFailed);
            Add(builder, bucket.StartAt, "outcome", "COMPLETED", bucket.Outcomes.Completed); Add(builder, bucket.StartAt, "outcome", "COMPLETED_BY_HUMAN", bucket.Outcomes.CompletedByHuman);
            Add(builder, bucket.StartAt, "outcome", "RESCHEDULED", bucket.Outcomes.Rescheduled); Add(builder, bucket.StartAt, "outcome", "CANCELLED", bucket.Outcomes.Cancelled);
            Add(builder, bucket.StartAt, "on_time_eligible", "", bucket.OnTime.Eligible); Add(builder, bucket.StartAt, "on_time", "", bucket.OnTime.OnTime);
            if (bucket.OnTime.Rate is { } rate) Add(builder, bucket.StartAt, "on_time_rate", "", rate);
            if (bucket.AverageDeliverySeconds is { } duration) Add(builder, bucket.StartAt, "average_delivery_seconds", "", duration);
            foreach (var item in bucket.MissionTypes) Add(builder, bucket.StartAt, "mission_type", item.Code, item.Total);
            foreach (var item in bucket.FailureReasons) Add(builder, bucket.StartAt, "failure_reason", item.Reason, item.Count);
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static byte[] Xlsx(OperationsReportResponse report)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).InsertTable(SummaryRows(report), "Summary", true);
        summary.Columns().AdjustToContents();
        var breakdown = workbook.Worksheets.Add("Breakdown");
        breakdown.Cell(1, 1).InsertTable(BreakdownRows(report), "Breakdown", true);
        breakdown.Columns().AdjustToContents();
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private static IEnumerable<object> SummaryRows(OperationsReportResponse report) => report.Buckets.Select(bucket => new
    {
        BucketStart = bucket.StartAt, bucket.Outcomes.Pending, bucket.Outcomes.Assigned, bucket.Outcomes.InProgress, bucket.Outcomes.DeliveryFailed,
        bucket.Outcomes.Completed, bucket.Outcomes.CompletedByHuman, bucket.Outcomes.Rescheduled, bucket.Outcomes.Cancelled,
        OnTimeEligible = bucket.OnTime.Eligible, OnTime = bucket.OnTime.OnTime, OnTimeRate = bucket.OnTime.Rate, bucket.AverageDeliverySeconds
    });
    private static IEnumerable<object> BreakdownRows(OperationsReportResponse report) => report.Buckets.SelectMany(bucket => bucket.MissionTypes.Select(item => new { BucketStart = bucket.StartAt, Metric = "MISSION_TYPE", Key = item.Code, Value = item.Total })
        .Concat(bucket.FailureReasons.Select(item => new { BucketStart = bucket.StartAt, Metric = "FAILURE_REASON", Key = item.Reason, Value = item.Count })));
    private static void Add(StringBuilder builder, DateTime bucket, string metric, string key, object value) => builder.Append(bucket.ToString("O")).Append(',').Append(metric).Append(',').Append(key).Append(',').Append(value).Append('\n');
}
