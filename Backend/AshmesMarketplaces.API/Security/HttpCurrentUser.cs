using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AshmesMarketplaces.Application.Auth.Security;

namespace AshmesMarketplaces.API.Security;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid? UserId => TryGetGuid(ClaimTypes.NameIdentifier);

    public Guid? RoleId => TryGetGuid("role_id");

    public int? SessionId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sid);
            return int.TryParse(value, out var sessionId) ? sessionId : null;
        }
    }

    private Guid? TryGetGuid(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
