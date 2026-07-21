namespace AshmesMarketplaces.Application.RecommendationProducts.Dtos;

public sealed class RecommendationProductListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public Guid? IdRecommendation { get; init; }
    public Guid? IdProduct { get; init; }
}
