namespace AshmesMarketplaces.Application.RecommendationCategories.Dtos;

public sealed class RecommendationCategoryListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public Guid? IdRecommendation { get; init; }
    public Guid? IdCategory { get; init; }
}
