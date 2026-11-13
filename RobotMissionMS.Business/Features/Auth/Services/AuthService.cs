using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Auth.DTOs;
using RobotMissionMS.Business.Features.Auth.Security;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Auth.Services;

/// <summary>
/// Use a request-scoped repository/context. A failed transaction ends this unit
/// of work; do not reuse its context to save staged entities in another request.
/// </summary>
public sealed class AuthService(
    IAuthRepository repository,
    IPasswordService passwords,
    IAccessTokenIssuer accessTokens,
    IRefreshTokenGenerator refreshTokens) : IAuthService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true))
            return LoginResult.Failure(LoginError.InvalidRequest);

        // Preserve the exact username/password; no implicit trimming or case folding.
        var snapshot = await repository.FindUserByUsernameAsync(request.Username, cancellationToken);
        if (snapshot is null)
            return LoginResult.Failure(LoginError.InvalidCredentials);

        var passwordResult = passwords.VerifyPassword(snapshot, request.Password);
        if (passwordResult == PasswordCheckResult.Failed)
            return LoginResult.Failure(LoginError.InvalidCredentials);
        if (!snapshot.IsActive)
            return LoginResult.Failure(LoginError.AccountInactive);

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var user = await repository.LockUserAsync(snapshot.Id, cancellationToken);
        if (user is null || !string.Equals(user.Username, request.Username, StringComparison.Ordinal))
            return LoginResult.Failure(LoginError.InvalidCredentials);

        // A password reset may have completed while this request waited for the lock.
        if (!string.Equals(user.PasswordHash, snapshot.PasswordHash, StringComparison.Ordinal))
            passwordResult = passwords.VerifyPassword(user, request.Password);
        if (passwordResult == PasswordCheckResult.Failed)
            return LoginResult.Failure(LoginError.InvalidCredentials);
        if (!user.IsActive)
            return LoginResult.Failure(LoginError.AccountInactive);

        if (passwordResult == PasswordCheckResult.SuccessRehashNeeded)
        {
            var hash = passwords.HashPassword(user, request.Password);
            if (await repository.UpdatePasswordHashAsync(user.Id, hash, cancellationToken) != 1)
                throw new InvalidOperationException("The locked user's password hash could not be updated.");
        }

        var familyId = Guid.NewGuid();
        var refresh = refreshTokens.Generate();
        var access = accessTokens.Issue(user, familyId);
        var response = new AuthResponse
        {
            AccessToken = access.Token,
            AccessTokenExpiresAt = access.ExpiresAt,
            RefreshToken = refresh.Token,
            RefreshTokenExpiresAt = refresh.ExpiresAt,
            User = new AuthUserResponse
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role.Name
            }
        };

        repository.AddRefreshToken(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            FamilyId = familyId,
            CreatedAt = refresh.CreatedAt.UtcDateTime,
            ExpiresAt = refresh.ExpiresAt.UtcDateTime
        });
        repository.AddLoginActivity(user.Id, familyId, refresh.CreatedAt.UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return LoginResult.Success(response);
    }
}
