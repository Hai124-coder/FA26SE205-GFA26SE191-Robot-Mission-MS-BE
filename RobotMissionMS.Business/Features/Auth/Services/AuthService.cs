using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Auth.Authorization;
using RobotMissionMS.Business.Features.Auth.DTOs;
using RobotMissionMS.Business.Features.Auth.Security;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Repositories.Interfaces;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

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
    RobotMissionDbContext db,
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
                Role = user.Role.Name,
                Floor = user.Zone?.Code
            },
            MustChangePassword = user.MustChangePassword
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
        await db.Users.Where(value => value.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.LastLoginAt, refresh.CreatedAt.UtcDateTime), cancellationToken);
        await db.PasswordResetRequests.Where(value => value.UserId == user.Id && value.Status == PasswordResetRequestStatus.Open)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, PasswordResetRequestStatus.Cancelled)
                .SetProperty(value => value.ProcessedAt, refresh.CreatedAt.UtcDateTime), cancellationToken);
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

        if (user.MustChangePassword)
            return RefreshResult.Failure(RefreshError.PasswordChangeRequired);

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
                Role = user.Role.Name,
                Floor = user.Zone?.Code
            },
            MustChangePassword = user.MustChangePassword
        });
    }

    public async Task<LogoutResult> LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true)
            || string.IsNullOrWhiteSpace(request.RefreshToken))
            return LogoutResult.Failure(LogoutError.InvalidRequest);

        var tokenHash = refreshTokens.ComputeHash(request.RefreshToken);
        var snapshot = await repository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (snapshot is null)
            return LogoutResult.Success();

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var user = await repository.LockUserAsync(snapshot.UserId, cancellationToken);
        if (user is null)
            return LogoutResult.Success();

        // Reread after the user lock to observe a concurrent refresh or logout.
        var current = await repository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (current is null || current.UserId != user.Id || current.RevokedAt is not null)
            return LogoutResult.Success();

        var now = clock.GetUtcNow().ToUniversalTime().UtcDateTime;
        if (await repository.RevokeFamilyAsync(user.Id, current.FamilyId, now, cancellationToken) == 0)
            return LogoutResult.Success();

        repository.AddLogoutActivity(user.Id, current.FamilyId, now);
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return LogoutResult.Success();
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return null;

        // Role and status may have changed since the access token was issued.
        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.Role is null || string.IsNullOrWhiteSpace(user.Role.Name))
            return null;

        var access = AuthScreenPolicy.ForRole(user.Role.Name);
        return new CurrentUserResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.Name,
            Floor = user.Zone?.Code,
            MustChangePassword = user.MustChangePassword,
            Screens = access.Screens,
            ReadOnlyScreens = access.ReadOnlyScreens
        };
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || request is null || !Validator.TryValidateObject(request, new ValidationContext(request), null, true))
            return ChangePasswordResult.Failure(ChangePasswordError.InvalidRequest);
        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var user = await repository.LockUserAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) return ChangePasswordResult.Failure(ChangePasswordError.UserNotFound);
        if (passwords.VerifyPassword(user, request.CurrentPassword) == PasswordCheckResult.Failed)
            return ChangePasswordResult.Failure(ChangePasswordError.WrongCurrentPassword);
        if (!IsStrongPassword(request.NewPassword, user.Username)) return ChangePasswordResult.Failure(ChangePasswordError.WeakPassword);
        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal)) return ChangePasswordResult.Failure(ChangePasswordError.PasswordSameAsOld);

        var now = clock.GetUtcNow().ToUniversalTime();
        await repository.UpdatePasswordHashAsync(user.Id, passwords.HashPassword(user, request.NewPassword), cancellationToken);
        await db.Users.Where(value => value.Id == user.Id).ExecuteUpdateAsync(setters => setters.SetProperty(value => value.MustChangePassword, false), cancellationToken);
        await repository.RevokeAllForUserAsync(user.Id, now.UtcDateTime, cancellationToken);
        var familyId = Guid.NewGuid(); var refresh = refreshTokens.Generate(); var access = accessTokens.Issue(user, familyId);
        repository.AddRefreshToken(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = refresh.TokenHash, FamilyId = familyId, CreatedAt = refresh.CreatedAt.UtcDateTime, ExpiresAt = refresh.ExpiresAt.UtcDateTime });
        db.ActivityLogs.Add(new ActivityLog { UserId = user.Id, Timestamp = now.UtcDateTime, Action = "PASSWORD_CHANGED", TargetObject = $"User:{user.Id:D}" });
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ChangePasswordResult.Success(new AuthResponse { AccessToken = access.Token, AccessTokenExpiresAt = access.ExpiresAt, RefreshToken = refresh.Token, RefreshTokenExpiresAt = refresh.ExpiresAt, MustChangePassword = false, User = new AuthUserResponse { Id = user.Id, Username = user.Username, FullName = user.FullName, Role = user.Role.Name, Floor = user.Zone?.Code } });
    }

    public async Task<ForgotPasswordResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || !Validator.TryValidateObject(request, new ValidationContext(request), null, true)) return ForgotPasswordResult.Invalid();
        var user = await db.Users.SingleOrDefaultAsync(value => value.Username == request.Username, cancellationToken);
        if (user is null || !user.IsActive) return ForgotPasswordResult.Accepted();
        var alreadyOpen = await db.PasswordResetRequests.AnyAsync(value => value.UserId == user.Id && value.Status == PasswordResetRequestStatus.Open, cancellationToken);
        if (alreadyOpen) return ForgotPasswordResult.Accepted();
        var now = clock.GetUtcNow().UtcDateTime;
        db.PasswordResetRequests.Add(new PasswordResetRequest { Id = Guid.NewGuid(), RequestCode = $"PR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), UserId = user.Id, Note = request.Note?.Trim(), Status = PasswordResetRequestStatus.Open, RequestedAt = now });
        db.ActivityLogs.Add(new ActivityLog { UserId = null, Timestamp = now, Action = "PASSWORD_RESET_REQUESTED", TargetObject = $"User:{user.Id:D}" });
        try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        return ForgotPasswordResult.Accepted();
    }

    private static bool IsStrongPassword(string password, string username) => password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit) && !string.Equals(password, username, StringComparison.OrdinalIgnoreCase);
}
