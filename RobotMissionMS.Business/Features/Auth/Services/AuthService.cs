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
    IRefreshTokenGenerator refreshTokens,
    TimeProvider clock) : IAuthService
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

    public async Task<RefreshResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.RefreshToken))
            return RefreshResult.Failure(RefreshError.InvalidRequest);

        var tokenHash = refreshTokens.ComputeHash(request.RefreshToken);
        var snapshot = await repository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (snapshot is null)
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var user = await repository.LockUserAsync(snapshot.UserId, cancellationToken);
        if (user is null)
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);

        // Read again after the user lock to see a refresh/logout committed while waiting.
        var current = await repository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (current is null || current.UserId != user.Id)
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);

        var now = clock.GetUtcNow().ToUniversalTime();
        if (!user.IsActive)
        {
            await repository.RevokeAllForUserAsync(user.Id, now.UtcDateTime, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RefreshResult.Failure(RefreshError.AccountInactive);
        }

        if (current.ExpiresAt <= now.UtcDateTime)
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);

        if (current.RevokedAt is not null)
        {
            // Reuse of an already rotated token indicates possible token theft.
            await repository.RevokeFamilyAsync(user.Id, current.FamilyId, now.UtcDateTime, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);
        }

        var refresh = refreshTokens.Generate();
        if (refresh.CreatedAt.UtcDateTime >= current.ExpiresAt)
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);

        var access = accessTokens.Issue(user, current.FamilyId);
        var replacementId = Guid.NewGuid();
        repository.AddRefreshToken(new RefreshToken
        {
            Id = replacementId,
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            FamilyId = current.FamilyId,
            CreatedAt = refresh.CreatedAt.UtcDateTime,
            // Rotation never extends the session's absolute expiry.
            ExpiresAt = current.ExpiresAt
        });
        repository.AddRefreshActivity(user.Id, current.FamilyId, refresh.CreatedAt.UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);
        if (await repository.MarkRefreshTokenReplacedAsync(user.Id, current.Id, replacementId,
                refresh.CreatedAt.UtcDateTime, cancellationToken) != 1)
            throw new InvalidOperationException("The refresh token could not be rotated after locking its user.");

        await transaction.CommitAsync(cancellationToken);
        return RefreshResult.Success(new AuthResponse
        {
            AccessToken = access.Token,
            AccessTokenExpiresAt = access.ExpiresAt,
            RefreshToken = refresh.Token,
            RefreshTokenExpiresAt = new DateTimeOffset(current.ExpiresAt),
            User = new AuthUserResponse
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role.Name
            }
        });
    }
}
