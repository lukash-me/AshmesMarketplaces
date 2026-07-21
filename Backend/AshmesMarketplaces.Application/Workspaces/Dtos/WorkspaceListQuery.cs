namespace AshmesMarketplaces.Application.Workspaces.Dtos;

public sealed class WorkspaceListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdBrand { get; init; }
    public int? Status { get; init; }
}
