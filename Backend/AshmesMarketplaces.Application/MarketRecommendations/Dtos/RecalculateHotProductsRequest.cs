namespace AshmesMarketplaces.Application.MarketRecommendations.Dtos;

public sealed record RecalculateHotProductsRequest(
    string? SourceCategory,
    string? SourceSubcategory,
    string? ProductParserRunId,
    string? RankParserRunId,
    int? MaxProducts,
    bool? ForceRecalculate,
    int? MaxRecommendations,
    decimal? MinConfidence,
    int? MinProductsForScoring);
