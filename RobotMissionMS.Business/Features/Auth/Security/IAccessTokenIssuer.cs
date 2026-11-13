using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.Business.Features.Auth.Security;

public interface IAccessTokenIssuer
{
    /// <summary>
    /// Issues a signed access token for a user whose Role is loaded.
    /// The calling Auth service must authenticate and authorize the user first.
    /// This operation does not create or persist a session.
    /// </summary>
    AccessTokenResult Issue(User user, Guid familyId);
}
