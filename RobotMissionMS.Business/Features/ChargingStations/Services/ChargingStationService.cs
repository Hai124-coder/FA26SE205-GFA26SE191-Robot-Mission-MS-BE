using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.ChargingStations.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.ChargingStations.Services;

public sealed class ChargingStationService(IChargingStationRepository repository) : IChargingStationService
{
    public async Task<ChargingStationListResult> GetChargingStationsAsync(ChargingStationListQuery query, string? operatorFloor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true)) return ChargingStationListResult.Invalid();
        var rows = await repository.GetChargingStationsAsync(operatorFloor?.Trim() ?? query.Floor?.Trim(), cancellationToken);
        var items = rows.Select(ToResponse).ToList();
        return ChargingStationListResult.Success(new PagedResponse<ChargingStationResponse>
        { Items = items.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), Page = query.Page, PageSize = query.PageSize, Total = items.Count });
    }

    public async Task<UpdateChargingStationResult> UpdateChargingStationAsync(Guid updatedByUserId, string locationCode,
        UpdateChargingStationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (updatedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(locationCode)
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, true))
            return UpdateChargingStationResult.Failure(UpdateChargingStationError.InvalidRequest);

        var matches = await repository.FindByLocationCodeAsync(locationCode.Trim(), cancellationToken);
        if (matches.Count == 0) return UpdateChargingStationResult.Failure(UpdateChargingStationError.NotFound);
        if (matches.Count > 1) return UpdateChargingStationResult.Failure(UpdateChargingStationError.AmbiguousCode);
        var result = await repository.UpdateCapacityAsync(matches[0].Id, request.Capacity, updatedByUserId, DateTime.UtcNow,
            cancellationToken);
        if (result == ChargingStationCapacityUpdateResult.CapacityBelowActiveSessions)
            return UpdateChargingStationResult.Failure(UpdateChargingStationError.CapacityBelowActiveSessions);

        var row = await repository.GetByIdAsync(matches[0].Id, cancellationToken)
            ?? throw new InvalidOperationException("The charging station disappeared after its update.");
        return UpdateChargingStationResult.Success(ToResponse(row));
    }

    private static ChargingStationResponse ToResponse(ChargingStationRow row) => new()
    {
        Id = row.Id,
        Code = row.LocationCode,
        Name = row.Name,
        Floor = row.FloorCode,
        FloorName = row.FloorName,
        StationType = row.StationType,
        Status = row.Status.ToString().ToUpperInvariant(),
        Capacity = row.Capacity,
        UsedPorts = row.ChargingRobots.Count,
        FreePorts = Math.Max(0, row.Capacity - row.ChargingRobots.Count),
        ChargingRobots = row.ChargingRobots.Select(robot => new ChargingRobotResponse
        {
            Id = robot.Id,
            RobotCode = robot.RobotCode,
            Battery = robot.Battery,
            StartedAt = robot.StartedAt
        }).ToList(),
        QueuedRobots = row.QueuedRobots.Select(robot => new QueuedRobotResponse
        {
            Id = robot.Id,
            RobotCode = robot.RobotCode,
            Battery = robot.Battery,
            QueuedAt = robot.QueuedAt
        }).ToList()
    };
}

public sealed class ChargingStationListResult
{
    private ChargingStationListResult(PagedResponse<ChargingStationResponse>? response) => Response = response;
    public PagedResponse<ChargingStationResponse>? Response { get; }
    public bool IsSuccess => Response is not null;
    public static ChargingStationListResult Success(PagedResponse<ChargingStationResponse> response) => new(response);
    public static ChargingStationListResult Invalid() => new(null);
}
