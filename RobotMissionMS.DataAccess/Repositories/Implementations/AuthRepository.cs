using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.DataAccess.Repositories.Implementations;

/// <summary>
/// Use one instance per DbContext/request; do not share concurrently between requests.
/// Token validity, password verification and token generation belong to the service.
/// </summary>
public sealed class AuthRepository(RobotMissionDbContext db) : IAuthRepository
{
    private Guid? lockTransactionId;
    private readonly HashSet<Guid> lockedUsers = [];

    public Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IAuthRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // ReadCommitted ensures a token reread AFTER waiting on the user lock sees
        // changes committed by the preceding refresh/logout request.
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        lockTransactionId = transaction.TransactionId;
        lockedUsers.Clear();
        return new AuthRepositoryTransaction(transaction);
    }

    public async Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var transaction = RequireTransaction();
        if (lockTransactionId != transaction.TransactionId)
        {
            lockTransactionId = transaction.TransactionId;
            lockedUsers.Clear();
        }

        // Interpolation is parameterized by EF. Do not concatenate user input.
        // Avoid tracking so an earlier read cannot return a stale cached user.
        var users = await db.Users.FromSqlInterpolated(
            $"""SELECT * FROM public."Users" WHERE "Id" = {userId} FOR UPDATE""")
            .AsNoTracking().ToListAsync(cancellationToken);
        var user = users.SingleOrDefault();
        if (user is null) return null;

        user.Role = await db.Roles.AsNoTracking().SingleAsync(x => x.Id == user.RoleId, cancellationToken);
        lockedUsers.Add(userId);
        return user;
    }

    public void AddRefreshToken(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        RequireUserLock(token.UserId);
        db.RefreshTokens.Add(token);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        return db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> UpdatePasswordHashAsync(Guid userId, string passwordHash,
        CancellationToken cancellationToken = default)
    {
        RequireUserLock(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (passwordHash.Length > 255)
            throw new ArgumentException("Password hash exceeds the database column limit.", nameof(passwordHash));

        return db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PasswordHash, passwordHash), cancellationToken);
    }

    public void AddLoginActivity(Guid userId, Guid familyId, DateTime occurredAtUtc)
        => AddAuthActivity(userId, familyId, occurredAtUtc, "AUTH_LOGIN");

    public void AddRefreshActivity(Guid userId, Guid familyId, DateTime occurredAtUtc)
        => AddAuthActivity(userId, familyId, occurredAtUtc, "AUTH_REFRESH");

    private void AddAuthActivity(Guid userId, Guid familyId, DateTime occurredAtUtc, string action)
    {
        RequireUserLock(userId);
        RequireUtc(occurredAtUtc);
        if (familyId == Guid.Empty)
            throw new ArgumentException("A session family ID is required.", nameof(familyId));

        // Do not accept free-form payloads that could accidentally contain secrets.
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Timestamp = occurredAtUtc,
            Action = action,
            TargetObject = $"AuthSession:{familyId:D}"
        });
    }

    public Task<int> MarkRefreshTokenReplacedAsync(Guid userId, Guid tokenId, Guid replacementTokenId,
        DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        RequireUserLock(userId);
        RequireUtc(revokedAtUtc);
        return db.RefreshTokens
            .Where(old => old.Id == tokenId && old.UserId == userId && old.RevokedAt == null
                && old.Id != replacementTokenId
                && db.RefreshTokens.Any(next => next.Id == replacementTokenId
                    && next.UserId == userId && next.FamilyId == old.FamilyId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, revokedAtUtc)
                .SetProperty(x => x.ReplacedByTokenId, replacementTokenId), cancellationToken);
    }

    public Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        RequireUserLock(userId);
        RequireUtc(revokedAtUtc);
        return db.RefreshTokens.Where(x => x.UserId == userId && x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, revokedAtUtc), cancellationToken);
    }

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        RequireUserLock(userId);
        RequireUtc(revokedAtUtc);
        return db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, revokedAtUtc), cancellationToken);
    }

    private IDbContextTransaction RequireTransaction() =>
        db.Database.CurrentTransaction
        ?? throw new InvalidOperationException("Begin an Auth transaction before locking or writing.");

    private void RequireUserLock(Guid userId)
    {
        var transaction = RequireTransaction();
        if (lockTransactionId != transaction.TransactionId || !lockedUsers.Contains(userId))
            throw new InvalidOperationException("Lock the user in the current transaction before changing Auth data.");
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must be UTC.", nameof(value));
    }

    private sealed class AuthRepositoryTransaction(IDbContextTransaction transaction) : IAuthRepositoryTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
