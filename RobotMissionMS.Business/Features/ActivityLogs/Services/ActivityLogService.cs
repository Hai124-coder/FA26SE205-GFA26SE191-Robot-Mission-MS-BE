using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.ActivityLogs.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.ActivityLogs.Services;

public sealed class ActivityLogService(RobotMissionDbContext db) : IActivityLogService
{
    private const int ExportLimit = 50_000;

    public async Task<ActivityLogPage> GetAsync(ActivityLogQuery query, CancellationToken cancellationToken = default)
    {
        var error = Validate(query);
        if (error is not null) return ActivityLogPage.Failure(error.Value);

        var logs = ApplyFilters(query);
        var total = await logs.CountAsync(cancellationToken);
        var items = await logs.Include(log => log.User)
            .OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return ActivityLogPage.Success(new PagedResponse<ActivityLogEntryResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        });
    }

    public async Task<ActivityLogExport> ExportAsync(ActivityLogQuery query, CancellationToken cancellationToken = default)
    {
        var error = Validate(query);
        if (error is not null) return ActivityLogExport.Failure(error.Value);

        var logs = await ApplyFilters(query).Include(log => log.User)
            .OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.Id)
            .Take(ExportLimit + 1).ToListAsync(cancellationToken);
        if (logs.Count > ExportLimit) return ActivityLogExport.Failure(ActivityLogQueryError.ExportLimitExceeded);
        return ActivityLogExport.Success(logs.Select(ToResponse).ToList());
    }

    private IQueryable<ActivityLog> ApplyFilters(ActivityLogQuery query)
    {
        var logs = db.ActivityLogs.AsNoTracking().AsQueryable();
        if (query.Actor is { } actor) logs = logs.Where(log => log.UserId == actor);
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToUpperInvariant();
            logs = logs.Where(log => log.Action.ToUpper() == action);
        }
        if (query.From is { } from) logs = logs.Where(log => log.Timestamp >= from);
        if (query.To is { } to) logs = logs.Where(log => log.Timestamp < EndExclusive(to));
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToUpperInvariant();
            logs = logs.Where(log => log.Action.ToUpper().Contains(term)
                || log.TargetObject.ToUpper().Contains(term)
                || (log.User != null && (log.User.Username.ToUpper().Contains(term) || log.User.FullName.ToUpper().Contains(term))));
        }
        return logs;
    }

    private static ActivityLogQueryError? Validate(ActivityLogQuery query)
    {
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true)
            || (query.From is { } from && query.To is { } to && from > to))
            return ActivityLogQueryError.InvalidQuery;
        if (query.From is { } start && query.To is { } end && EndExclusive(end) - start > TimeSpan.FromDays(93))
            return ActivityLogQueryError.RangeTooLarge;
        return null;
    }

    private static DateTime EndExclusive(DateTime value) => value.TimeOfDay == TimeSpan.Zero ? value.Date.AddDays(1) : value;
    private static ActivityLogEntryResponse ToResponse(ActivityLog log) => new()
    {
        Id = log.Id,
        Timestamp = log.Timestamp,
        Actor = log.User is null ? null : new ActivityLogActorResponse { Id = log.User.Id, Username = log.User.Username, FullName = log.User.FullName },
        Action = log.Action,
        TargetObject = log.TargetObject,
        Message = $"{log.Action} – {log.TargetObject}"
    };
}
