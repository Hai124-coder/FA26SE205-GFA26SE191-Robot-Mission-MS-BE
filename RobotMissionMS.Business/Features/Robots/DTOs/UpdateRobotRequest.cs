namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Request body for PUT /robots/{id}.
/// Only HomeChargingStationId is updatable because the Notes column
/// does not exist in the current schema.
/// </summary>
public sealed class UpdateRobotRequest
{
    /// <summary>
    /// ID of the new home charging station, or null to unassign.
    /// </summary>
    public Guid? HomeChargingStationId { get; init; }
}
