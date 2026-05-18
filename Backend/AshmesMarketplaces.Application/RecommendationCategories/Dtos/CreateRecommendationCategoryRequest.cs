namespace AshmesMarketplaces.Application.RecommendationCategories.Dtos;

public sealed class CreateRecommendationCategoryRequest
{
    public Guid IdRecommendation { get; init; }
    public Guid IdCategory { get; init; }
}
