namespace AshmesMarketplaces.Application.Permissions.Dtos;

public sealed class PermissionListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdCategory { get; init; }
    public int? Domain { get; init; }
}
