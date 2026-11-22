using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Features.Reports.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Reports.Services;

public sealed class OperationsReportService(RobotMissionDbContext db) : IOperationsReportService
{
    public async Task<OperationsReportResult> GetAsync(OperationsReportQuery query, CancellationToken cancellationToken = default)
    {
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true) || query.From is null || query.To is null || query.From > query.To)
            return OperationsReportResult.Failure(OperationsReportError.InvalidQuery);
        var start = query.From.Value;
        var end = EndExclusive(query.To.Value);
        if (end <= start) return OperationsReportResult.Failure(OperationsReportError.InvalidQuery);
        if (end - start > TimeSpan.FromDays(93)) return OperationsReportResult.Failure(OperationsReportError.RangeTooLarge);
        var groupBy = query.GroupBy.Trim().ToLowerInvariant();

        var missions = db.Missions.AsNoTracking().Include(x => x.MissionType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Floor)) missions = missions.Where(x => x.Zone.Code == query.Floor.Trim());
        var rows = await missions.Where(x => (x.CompletedAt ?? x.CreatedAt) >= start && (x.CompletedAt ?? x.CreatedAt) < end)
            .Select(x => new Row(x.MissionType.Code, x.Status, x.FailReason, x.CreatedAt, x.StartedAt, x.CompletedAt, x.DeadlineAt))
            .ToListAsync(cancellationToken);

        var buckets = rows.GroupBy(x => BucketStart(x.CompletedAt ?? x.CreatedAt, groupBy)).OrderBy(x => x.Key)
            .Select(group => ToBucket(group.Key, group.ToList())).ToList();
        return OperationsReportResult.Success(new OperationsReportResponse { From = start, To = query.To.Value, Floor = Text(query.Floor), GroupBy = groupBy, Buckets = buckets });
    }

    private static OperationsReportBucket ToBucket(DateTime startAt, IReadOnlyList<Row> rows)
    {
        var completed = new[] { MissionStatus.Completed, MissionStatus.CompletedByHuman };
        var onTimeRows = rows.Where(x => completed.Contains(x.Status) && x.DeadlineAt is not null && x.CompletedAt is not null).ToList();
        var onTime = onTimeRows.Count(x => x.CompletedAt <= x.DeadlineAt);
        var durations = rows.Where(x => x.Status == MissionStatus.Completed && x.StartedAt is not null && x.CompletedAt is not null && x.CompletedAt >= x.StartedAt)
            .Select(x => (x.CompletedAt!.Value - x.StartedAt!.Value).TotalSeconds).ToList();
        return new OperationsReportBucket
        {
            StartAt = startAt,
            MissionTypes = rows.GroupBy(x => x.MissionTypeCode).OrderBy(x => x.Key).Select(x => new OperationsMissionTypeCount(x.Key, x.Count())).ToList(),
            Outcomes = new OperationsOutcomeCounts
            {
                Pending = Count(rows, MissionStatus.Pending), Assigned = Count(rows, MissionStatus.Assigned), InProgress = Count(rows, MissionStatus.InProgress),
                DeliveryFailed = Count(rows, MissionStatus.DeliveryFailed), Completed = Count(rows, MissionStatus.Completed), CompletedByHuman = Count(rows, MissionStatus.CompletedByHuman),
                Rescheduled = Count(rows, MissionStatus.Rescheduled), Cancelled = Count(rows, MissionStatus.Cancelled)
            },
            OnTime = new OperationsOnTimeMetrics { Eligible = onTimeRows.Count, OnTime = onTime, Rate = onTimeRows.Count == 0 ? null : Math.Round((decimal)onTime / onTimeRows.Count, 4) },
            AverageDeliverySeconds = durations.Count == 0 ? null : Math.Round(durations.Average(), 2),
            FailureReasons = rows.Where(x => x.Status == MissionStatus.DeliveryFailed && x.FailReason is not null).GroupBy(x => Code(x.FailReason!.Value)).OrderBy(x => x.Key).Select(x => new OperationsFailureReasonCount(x.Key, x.Count())).ToList(),
            CompletedByHumanCount = Count(rows, MissionStatus.CompletedByHuman)
        };
    }

    private static int Count(IEnumerable<Row> rows, MissionStatus status) => rows.Count(x => x.Status == status);
    private static DateTime BucketStart(DateTime value, string groupBy) => groupBy == "hour" ? new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc) : value.Date;
    private static DateTime EndExclusive(DateTime value) => value.TimeOfDay == TimeSpan.Zero ? value.Date.AddDays(1) : value;
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Code(MissionFailReason value) => value switch { MissionFailReason.NoRecipient => "NO_RECIPIENT", MissionFailReason.LocationClosed => "LOCATION_CLOSED", _ => "OTHER" };
    private sealed record Row(string MissionTypeCode, MissionStatus Status, MissionFailReason? FailReason, DateTime CreatedAt, DateTime? StartedAt, DateTime? CompletedAt, DateTime? DeadlineAt);
}
