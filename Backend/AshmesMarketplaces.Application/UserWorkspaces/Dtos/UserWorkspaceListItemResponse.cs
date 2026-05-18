namespace AshmesMarketplaces.Application.UserWorkspaces.Dtos;

public sealed record UserWorkspaceListItemResponse(
    Guid IdUser,
    Guid IdWorkspace,
    Guid IdRole);
