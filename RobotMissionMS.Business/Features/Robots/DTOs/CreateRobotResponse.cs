namespace RobotMissionMS.Business.Features.Robots.DTOs;

/// <summary>
/// Response for POST /robots — the newly created robot.
/// </summary>
public sealed class CreateRobotResponse
{
    public required Guid    Id                   { get; init; }
    public required string  RobotCode            { get; init; }
    public required string  Type                 { get; init; }
    public required string  TypeName             { get; init; }
    public required string  Floor                { get; init; }
    public required string  FloorName            { get; init; }
    public required string  State                { get; init; }
    public required bool    IsConnected          { get; init; }
    public required int     Battery              { get; init; }
    public string?          HomeStationCode      { get; init; }
    public required DateTime CreatedAt           { get; init; }
}
