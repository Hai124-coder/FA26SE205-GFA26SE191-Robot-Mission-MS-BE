namespace RobotMissionMS.Business.Features.Auth.Authorization;

/// <summary>
/// Menu access transcribed from RMMS v8 system scope, section 2. This is kept
/// separate from endpoint authorization; the latter will be added per module.
/// </summary>
public static class AuthScreenPolicy
{
    public static ScreenAccess ForRole(string role) => role switch
    {
        "OPERATOR" => new(
            ["SC-13", "SC-10", "SC-12", "SC-11", "SC-17", "SC-16", "SC-15", "SC-19", "SC-18"],
            ["SC-18"]),
        "SUPERVISOR" => new(
            ["SC-20", "SC-13", "SC-17", "SC-12", "SC-16", "SC-15", "SC-18", "SC-23", "SC-21", "SC-22", "SC-19", "SC-07"],
            ["SC-13", "SC-17", "SC-12", "SC-16", "SC-15", "SC-18", "SC-19"]),
        "ADMIN" => new(
            ["SC-03", "SC-04", "SC-05", "SC-08", "SC-06", "SC-18", "SC-02", "SC-07"],
            Array.Empty<string>()),
        _ => new(Array.Empty<string>(), Array.Empty<string>())
    };
}

public sealed record ScreenAccess(IReadOnlyList<string> Screens, IReadOnlyList<string> ReadOnlyScreens);
