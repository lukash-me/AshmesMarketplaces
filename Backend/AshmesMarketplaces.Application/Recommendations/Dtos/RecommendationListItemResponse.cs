namespace AshmesMarketplaces.Application.Recommendations.Dtos;

public sealed record RecommendationListItemResponse(
    Guid Id,
    Guid IdModel,
    int Type,
    int TypeObject,
    decimal Score,
    DateTime DateCreate,
    DateTime DateUpdate);
