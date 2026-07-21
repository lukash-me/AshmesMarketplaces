namespace AshmesMarketplaces.Application.Categories.Dtos;

public sealed record CategoryListItemResponse(
    Guid Id,
    Guid? IdParentCategory,
    string? IdOnMp,
    string Name,
    int Level,
    bool IsActive,
    DateTime DateUpdate);
