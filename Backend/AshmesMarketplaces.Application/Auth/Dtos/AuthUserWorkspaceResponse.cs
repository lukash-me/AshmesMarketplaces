namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record AuthUserWorkspaceResponse(
    Guid IdWorkspace,
    Guid IdRole,
    string WorkspaceName);
