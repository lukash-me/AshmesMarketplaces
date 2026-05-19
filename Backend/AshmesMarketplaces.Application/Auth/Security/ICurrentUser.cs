namespace AshmesMarketplaces.Application.Auth.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? RoleId { get; }
    int? SessionId { get; }
}
