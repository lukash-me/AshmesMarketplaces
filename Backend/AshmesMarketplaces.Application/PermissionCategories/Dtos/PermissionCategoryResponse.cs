namespace AshmesMarketplaces.Application.PermissionCategories.Dtos;

public sealed record PermissionCategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateCreate,
    DateTime DateUpdate);
