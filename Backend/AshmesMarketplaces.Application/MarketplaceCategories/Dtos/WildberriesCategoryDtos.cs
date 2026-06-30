namespace AshmesMarketplaces.Application.MarketplaceCategories.Dtos;

public sealed record WildberriesCategoryNodeDto(
    long Id,
    string Name,
    string SourceCategory,
    string SourceSubcategory,
    string Path,
    string? SearchQuery,
    long? ParentId,
    bool IsLeaf,
    int Level);

public sealed record WildberriesCategoryCatalogDto(
    string Marketplace,
    DateTime FetchedAtUtc,
    IReadOnlyList<WildberriesCategoryNodeDto> Nodes);

public sealed record WildberriesCategorySearchQuery(string? Query);
