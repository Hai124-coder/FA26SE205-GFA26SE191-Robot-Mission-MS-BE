namespace RobotMissionMS.Business.Features.Auth.DTOs;

public sealed class CurrentUserResponse : AuthUserResponse
{
    public IReadOnlyList<string> Screens { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ReadOnlyScreens { get; init; } = Array.Empty<string>();
}
