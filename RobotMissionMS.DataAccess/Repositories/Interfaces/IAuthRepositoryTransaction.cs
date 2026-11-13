namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

/// <summary>
/// Disposing without committing rolls back. Commit does not call SaveChangesAsync.
/// </summary>
public interface IAuthRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
