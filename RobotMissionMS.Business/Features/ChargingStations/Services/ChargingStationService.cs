using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.ChargingStations.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.ChargingStations.Services;

public sealed class ChargingStationService(IChargingStationRepository repository) : IChargingStationService
{
    public async Task<IReadOnlyList<ChargingStationResponse>> GetChargingStationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetChargingStationsAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
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
        }).ToList()
    };
}
