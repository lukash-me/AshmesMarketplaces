namespace AshmesMarketplaces.Application.RecommendationProducts.Dtos;

public sealed class CreateRecommendationProductRequest
{
    public Guid IdRecommendation { get; init; }
    public Guid IdProduct { get; init; }
}
