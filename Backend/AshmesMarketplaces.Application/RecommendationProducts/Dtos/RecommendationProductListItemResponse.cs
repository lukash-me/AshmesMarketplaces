namespace AshmesMarketplaces.Application.RecommendationProducts.Dtos;

public sealed record RecommendationProductListItemResponse(
    Guid IdRecommendation,
    Guid IdProduct);
