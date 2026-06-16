namespace AshmesMarketplaces.Application.Auth.Dtos;

public sealed record AuthUserResponse(
    Guid Id,
    Guid IdRole,
    string Login,
    string? Email,
    string Phone,
    int Status,
    AuthAnalysisScheduleResponse AnalysisSchedule,
    IReadOnlyCollection<AuthUserWorkspaceResponse> Workspaces,
    IReadOnlyCollection<AuthPermissionResponse> Permissions);
