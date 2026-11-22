using RobotMissionMS.Business.Features.Reports.DTOs;

namespace RobotMissionMS.Business.Features.Reports.Services;

public interface IOperationsReportService
{
    Task<OperationsReportResult> GetAsync(OperationsReportQuery query, CancellationToken cancellationToken = default);
}

public enum OperationsReportError { InvalidQuery, RangeTooLarge }
public sealed record OperationsReportResult(OperationsReportResponse? Response, OperationsReportError? Error)
{
    public bool IsSuccess => Error is null;
    public static OperationsReportResult Success(OperationsReportResponse response) => new(response, null);
    public static OperationsReportResult Failure(OperationsReportError error) => new(null, error);
}
