using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.ActivityLogs.DTOs;
using RobotMissionMS.Business.Features.ActivityLogs.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/logs")]
[Authorize(Policy = AuthPolicies.SupervisorOrAdmin)]
public sealed class LogsController(IActivityLogService logs) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ActivityLogEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ActivityLogEntryResponse>>> List([FromQuery] ActivityLogQuery query, CancellationToken cancellationToken)
    {
        var result = await logs.GetAsync(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Response) : Failure(result.Error!.Value);
    }

    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Export([FromQuery] ActivityLogQuery query, CancellationToken cancellationToken)
    {
        var result = await logs.ExportAsync(query, cancellationToken);
        return result.IsSuccess
            ? File(Csv(result.Items!), "text/csv; charset=utf-8", "activity-logs.csv")
            : Failure(result.Error!.Value);
    }

    private ActionResult Failure(ActivityLogQueryError error) => error switch
    {
        ActivityLogQueryError.InvalidQuery => Problem(type: "ACTIVITY_LOG_INVALID_QUERY", statusCode: StatusCodes.Status400BadRequest),
        ActivityLogQueryError.RangeTooLarge => Problem(type: "ACTIVITY_LOG_RANGE_TOO_LARGE", statusCode: StatusCodes.Status400BadRequest),
        ActivityLogQueryError.ExportLimitExceeded => Problem(type: "LOG_EXPORT_LIMIT_EXCEEDED", statusCode: StatusCodes.Status409Conflict),
        _ => throw new InvalidOperationException("Activity log service returned an invalid result.")
    };

    private static byte[] Csv(IEnumerable<ActivityLogEntryResponse> items)
    {
        var builder = new StringBuilder("id,timestamp,actor_id,username,full_name,action,target_object,message\n");
        foreach (var item in items)
            builder.Append(item.Id).Append(',').Append(item.Timestamp.ToString("O")).Append(',')
                .Append(Text(item.Actor?.Id.ToString())).Append(',').Append(Text(item.Actor?.Username)).Append(',')
                .Append(Text(item.Actor?.FullName)).Append(',').Append(Text(item.Action)).Append(',')
                .Append(Text(item.TargetObject)).Append(',').Append(Text(item.Message)).Append('\n');
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static string Text(string? value) => value is null ? string.Empty : $"\"{value.Replace("\"", "\"\"")}\"";
}
