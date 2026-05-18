namespace AshmesMarketplaces.Application.UserWorkspaces.Dtos;

public sealed record UserWorkspaceResponse(
    Guid IdUser,
    Guid IdWorkspace,
    Guid IdRole);
