namespace AshmesMarketplaces.Application.Roles.Dtos;

public sealed class RoleListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
}
