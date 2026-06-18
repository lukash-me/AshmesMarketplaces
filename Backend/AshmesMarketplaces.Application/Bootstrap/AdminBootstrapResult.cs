namespace AshmesMarketplaces.Application.Bootstrap;

public sealed record AdminBootstrapResult(
    string Login,
    Guid UserId,
    Guid RoleId,
    Guid WorkspaceId,
    bool RoleCreated,
    bool UserCreated,
    bool PasswordUpdated,
    bool WorkspaceCreated,
    bool MembershipCreated);
