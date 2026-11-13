namespace RobotMissionMS.Business.Features.Auth.DTOs;

public class AuthUserResponse
{
    public required Guid Id { get; init; }

    public required string Username { get; init; }

    public required string FullName { get; init; }

    public required string Role { get; init; }
}