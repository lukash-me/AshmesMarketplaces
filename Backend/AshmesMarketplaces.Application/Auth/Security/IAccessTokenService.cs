using AshmesMarketplaces.Domain.Entities.Users;

namespace AshmesMarketplaces.Application.Auth.Security;

public interface IAccessTokenService
{
    AccessTokenResult CreateAccessToken(User user, int sessionId, DateTime nowUtc);
}
