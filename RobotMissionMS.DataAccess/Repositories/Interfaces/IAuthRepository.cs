using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

/// <summary>
/// Reads return detached snapshots. Before changing tokens, begin a transaction,
/// lock the user, then read the token again to obtain its current state.
/// </summary>
public interface IAuthRepository
{
    Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IAuthRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds a PostgreSQL row lock until the transaction ends. Includes the role.
    /// Returns null if the user does not exist.
    /// </summary>
    Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an insert; call SaveChangesAsync explicitly. Requires a user lock.
    /// </summary>
    void AddRefreshToken(RefreshToken token);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately updates only PasswordHash in the current transaction.
    /// Requires a user lock. The service supplies an already-generated hash.
    /// Detached user snapshots are not updated; read again if needed.
    /// </summary>
    Task<int> UpdatePasswordHashAsync(Guid userId, string passwordHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a successful-login audit entry containing only user and session IDs.
    /// Requires a user lock; call SaveChangesAsync explicitly before committing.
    /// </summary>
    void AddLoginActivity(Guid userId, Guid familyId, DateTime occurredAtUtc);

    /// <summary>
    /// Stages a successful refresh audit entry containing only user and session IDs.
    /// Requires a user lock; call SaveChangesAsync explicitly before committing.
    /// </summary>
    void AddRefreshActivity(Guid userId, Guid familyId, DateTime occurredAtUtc);

    /// <summary>
    /// Stages a logout audit entry containing only user and session IDs.
    /// Requires a user lock; call SaveChangesAsync explicitly before committing.
    /// </summary>
    void AddLogoutActivity(Guid userId, Guid familyId, DateTime occurredAtUtc);

    /// <summary>
    /// Immediately updates an unrevoked token in the current transaction.
    /// The replacement must already be saved and belong to the same user/family.
    /// Returns 0 if no matching update is possible; the service decides what to do.
    /// </summary>
    Task<int> MarkRefreshTokenReplacedAsync(Guid userId, Guid tokenId, Guid replacementTokenId,
        DateTime revokedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately revokes unrevoked tokens in this family, within the current transaction.
    /// </summary>
    Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately revokes all unrevoked tokens for this user, within the current transaction.
    /// </summary>
    Task<int> RevokeAllForUserAsync(Guid userId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}
