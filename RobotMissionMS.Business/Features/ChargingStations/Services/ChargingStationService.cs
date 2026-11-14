using RobotMissionMS.Business.Features.ChargingStations.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.ChargingStations.Services;

public sealed class ChargingStationService(IChargingStationRepository repository) : IChargingStationService
{
    public async Task<IReadOnlyList<ChargingStationResponse>> GetChargingStationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetChargingStationsAsync(cancellationToken);
        return rows.Select(row => new ChargingStationResponse
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
        }).ToList();
    }
}
