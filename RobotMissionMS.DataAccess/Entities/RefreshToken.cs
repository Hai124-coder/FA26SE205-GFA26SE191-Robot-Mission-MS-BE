namespace RobotMissionMS.DataAccess.Entities;

/// <summary>
/// Refresh token hashes and rotation history for a login session.
/// </summary>
public partial class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 uppercase hexadecimal hash. Never store the original token.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// Shared by all tokens issued through rotation within the same session.
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Absolute session expiry, preserved when rotating tokens.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual RefreshToken? ReplacedByToken { get; set; }

    public virtual ICollection<RefreshToken> InverseReplacedByToken { get; set; } = new List<RefreshToken>();
}
