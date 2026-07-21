namespace AshmesMarketplaces.Application.RolePermissions.Dtos;

public sealed class RolePermissionListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public Guid? IdRole { get; init; }
    public Guid? IdPermission { get; init; }
}
