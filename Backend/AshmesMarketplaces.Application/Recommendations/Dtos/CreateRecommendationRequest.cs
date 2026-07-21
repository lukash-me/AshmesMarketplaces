using System.Text.Json;

namespace AshmesMarketplaces.Application.Recommendations.Dtos;

public sealed class CreateRecommendationRequest
{
    public Guid IdModel { get; init; }
    public int Type { get; init; }
    public int TypeObject { get; init; }
    public decimal Score { get; init; }
    public JsonElement? Explanation { get; init; }
    public JsonElement? Snapshot { get; init; }
    public DateTime DateCreate { get; init; }
    public DateTime DateUpdate { get; init; }
}
