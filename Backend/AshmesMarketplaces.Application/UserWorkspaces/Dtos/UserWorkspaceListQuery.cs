namespace AshmesMarketplaces.Application.UserWorkspaces.Dtos;

public sealed class UserWorkspaceListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdUser { get; init; }
    public Guid? IdWorkspace { get; init; }
    public Guid? IdRole { get; init; }
}
