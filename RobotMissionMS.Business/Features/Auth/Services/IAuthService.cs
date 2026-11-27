using RobotMissionMS.Business.Features.Auth.DTOs;

namespace RobotMissionMS.Business.Features.Auth.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<RefreshResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<LogoutResult> LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}
