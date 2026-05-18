using System.Text.Json;

namespace AshmesMarketplaces.Application.Recommendations.Dtos;

public sealed record RecommendationResponse(
    Guid Id,
    Guid IdModel,
    int Type,
    int TypeObject,
    decimal Score,
    JsonElement? Explanation,
    JsonElement? Snapshot,
    DateTime DateCreate,
    DateTime DateUpdate);
