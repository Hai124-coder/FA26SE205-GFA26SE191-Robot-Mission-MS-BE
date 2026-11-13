using Microsoft.AspNetCore.Identity;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.Business.Features.Auth.Security;

/// <summary>
/// Uses ASP.NET Core Identity's password format and verification.
/// Does not generate tokens, authorize users or access the database.
/// </summary>
public sealed class PasswordService(IPasswordHasher<User> passwordHasher) : IPasswordService
{
    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(password);

        // Do not trim or otherwise normalize a password.
        return passwordHasher.HashPassword(user, password);
    }

    public PasswordCheckResult VerifyPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(user.PasswordHash))
            return PasswordCheckResult.Failed;

        try
        {
            return passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheckResult.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
                _ => PasswordCheckResult.Failed
            };
        }
        catch (FormatException)
        {
            // A malformed stored hash cannot authenticate a user.
            // Never include the supplied password or stored hash in an error.
            return PasswordCheckResult.Failed;
        }
    }
}
