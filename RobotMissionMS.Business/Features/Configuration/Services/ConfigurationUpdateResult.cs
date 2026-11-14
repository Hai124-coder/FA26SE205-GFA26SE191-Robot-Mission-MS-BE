using RobotMissionMS.Business.Features.Configuration.DTOs;

namespace RobotMissionMS.Business.Features.Configuration.Services;

public enum ConfigurationUpdateError { InvalidRequest }

public sealed class ConfigurationUpdateResult
{
    private ConfigurationUpdateResult(ConfigurationResponse? response, ConfigurationUpdateError? error,
        IReadOnlyList<string>? errors)
    { Response = response; Error = error; Errors = errors ?? Array.Empty<string>(); }

    public ConfigurationResponse? Response { get; }
    public ConfigurationUpdateError? Error { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Error is null;
    public static ConfigurationUpdateResult Success(ConfigurationResponse response) => new(response, null, null);
    public static ConfigurationUpdateResult Failure(IReadOnlyList<string> errors) => new(null, ConfigurationUpdateError.InvalidRequest, errors);
}
