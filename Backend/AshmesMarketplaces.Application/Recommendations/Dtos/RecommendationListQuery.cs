namespace AshmesMarketplaces.Application.Recommendations.Dtos;

public sealed class RecommendationListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public Guid? IdModel { get; init; }
    public int? Type { get; init; }
    public int? TypeObject { get; init; }
    public DateTime? DateCreateFrom { get; init; }
    public DateTime? DateCreateTo { get; init; }
}
