namespace AshmesMarketplaces.Application.PermissionCategories.Dtos;

public sealed record PermissionCategoryListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
