namespace AshmesMarketplaces.Application.PermissionCategories.Dtos;

public sealed class UpdatePermissionCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
