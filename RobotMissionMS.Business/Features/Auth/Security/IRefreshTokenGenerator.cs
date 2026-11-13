namespace RobotMissionMS.Business.Features.Auth.Security;

public interface IRefreshTokenGenerator
{
    /// <summary>
    /// Generates a token for a new login session using the configured lifetime.
    /// Does not create an entity, assign a user/family or save anything.
    /// Future token rotation must preserve the original session expiry.
    /// </summary>
    RefreshTokenResult Generate();

    /// <summary>
    /// Hashes the exact supplied token for a database lookup. Does not validate
    /// whether the token exists, has expired or has been revoked.
    /// </summary>
    string ComputeHash(string token);
}
