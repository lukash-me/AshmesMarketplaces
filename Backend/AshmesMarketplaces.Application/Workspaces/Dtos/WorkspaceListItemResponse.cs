namespace AshmesMarketplaces.Application.Workspaces.Dtos;

public sealed record WorkspaceListItemResponse(
    Guid Id,
    Guid? IdBrand,
    string Name,
    string? Description,
    string? UrlInvite,
    int Status,
    DateTime DateCreate,
    DateTime DateUpdate);
