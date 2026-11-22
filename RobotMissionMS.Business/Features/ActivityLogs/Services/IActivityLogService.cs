using RobotMissionMS.Business.Features.ActivityLogs.DTOs;

namespace RobotMissionMS.Business.Features.ActivityLogs.Services;

public interface IActivityLogService
{
    Task<ActivityLogPage> GetAsync(ActivityLogQuery query, CancellationToken cancellationToken = default);
    Task<ActivityLogExport> ExportAsync(ActivityLogQuery query, CancellationToken cancellationToken = default);
}
