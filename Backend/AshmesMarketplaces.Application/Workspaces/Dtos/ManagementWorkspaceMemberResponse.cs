namespace AshmesMarketplaces.Application.Workspaces.Dtos;

public sealed record ManagementWorkspaceMemberResponse(
    Guid IdUser,
    string Login,
    string? Email,
    Guid IdRole,
    string RoleName,
    bool IsCurrentUser);
