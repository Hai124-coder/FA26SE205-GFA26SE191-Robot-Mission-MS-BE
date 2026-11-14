using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Robots.Services;

public sealed class RobotService(IRobotRepository repository) : IRobotService
{
    public async Task<RobotListResult> GetRobotsAsync(
        RobotListQuery    query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, validateAllProperties: true))
            return RobotListResult.Failure(RobotListError.InvalidQuery);

        // Parse optional State filter string → enum
        RobotState? stateFilter = null;
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            if (!TryParseRobotState(query.State, out var parsed))
                return RobotListResult.Failure(RobotListError.InvalidQuery);
            stateFilter = parsed;
        }

        var rows = await repository.GetRobotsAsync(
            string.IsNullOrWhiteSpace(query.Floor) ? null : query.Floor,
            string.IsNullOrWhiteSpace(query.Type)  ? null : query.Type,
            stateFilter,
            query.IsConnected,
            cancellationToken);

        var response = rows
            .Select(r => new RobotListItemResponse
            {
                Id              = r.Id,
                RobotCode       = r.RobotCode,
                Type            = r.TypeCode,
                TypeName        = r.TypeName,
                Floor           = r.FloorCode,
                FloorName       = r.FloorName,
                State           = RobotStateToString(r.State),
                IsConnected     = r.IsConnected,
                Battery         = r.Battery,
                CurrentX        = r.CurrentX,
                CurrentY        = r.CurrentY,
                ActiveTripCode  = r.ActiveTripCode,
                LastHeartbeatAt = r.LastHeartbeatAt,
            })
            .ToList();

        return RobotListResult.Success(response);
    }

    // Keep string values aligned with the PgName / API doc enum labels.
    private static bool TryParseRobotState(string value, out RobotState result)
    {
        result = value.ToUpperInvariant() switch
        {
            "INACTIVE"    => RobotState.Inactive,
            "AVAILABLE"   => RobotState.Available,
            "ON_TRIP"     => RobotState.OnTrip,
            "CHARGING"    => RobotState.Charging,
            "ERROR"       => RobotState.Error,
            "MAINTENANCE" => RobotState.Maintenance,
            _             => (RobotState)(-1)
        };
        return (int)result >= 0;
    }

    private static string RobotStateToString(RobotState state) => state switch
    {
        RobotState.Inactive    => "INACTIVE",
        RobotState.Available   => "AVAILABLE",
        RobotState.OnTrip      => "ON_TRIP",
        RobotState.Charging    => "CHARGING",
        RobotState.Error       => "ERROR",
        RobotState.Maintenance => "MAINTENANCE",
        _                      => state.ToString().ToUpperInvariant()
    };
}
