namespace AshmesMarketplaces.Application.MarketRecommendations.Dtos;

public sealed class HotProductsListQuery
{
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? GroupKey { get; init; }
    public IReadOnlyList<string> FactorKeys { get; init; } = [];
    public string? FactorMode { get; init; }
    public string? WbProductId { get; init; }
    public string? WbRootId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
