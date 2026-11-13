using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.Business.Features.Auth.Security;

public interface IPasswordService
{
    /// <summary>
    /// Creates a salted hash. Does not change the user or save anything.
    /// Password creation policy must be checked by the calling service.
    /// </summary>
    string HashPassword(User user, string password);

    /// <summary>
    /// Compares the supplied password with User.PasswordHash.
    /// A rehash result means verification succeeded, but the calling service
    /// should generate and persist an upgraded hash.
    /// </summary>
    PasswordCheckResult VerifyPassword(User user, string password);
}
