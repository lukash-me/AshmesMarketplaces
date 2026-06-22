namespace AshmesMarketplaces.Application.Workspaces.Dtos;

public sealed record ManagementWorkspaceResponse(
    Guid Id,
    Guid? IdBrand,
    string Name,
    string? Description,
    int Status,
    DateTime DateCreate,
    DateTime DateUpdate,
    IReadOnlyCollection<ManagementWorkspaceMemberResponse> Members);
