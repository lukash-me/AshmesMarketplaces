namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record AuthUserResponse(
    Guid Id,
    Guid IdRole,
    string Login,
    string? Email,
    string Phone,
    int Status,
    IReadOnlyCollection<AuthUserWorkspaceResponse> Workspaces,
    IReadOnlyCollection<AuthPermissionResponse> Permissions);
