using RobotMissionMS.Business.Features.Configuration.DTOs;

namespace RobotMissionMS.Business.Features.Configuration.Services;

public interface IConfigurationService
{
    Task<ConfigurationResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<ConfigurationUpdateResult> UpdateAsync(Guid updatedByUserId, UpdateConfigurationRequest request,
        CancellationToken cancellationToken = default);
}
