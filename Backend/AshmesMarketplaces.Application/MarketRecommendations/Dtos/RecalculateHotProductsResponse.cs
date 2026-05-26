namespace AshmesMarketplaces.Application.MarketRecommendations.Dtos;

public sealed record RecalculateHotProductsResponse(
    Guid RunId,
    string Status,
    string Algorithm,
    string AlgorithmVersion,
    string ModelVersion,
    string InputSnapshotHash,
    string? ProductParserRunId,
    string? RankParserRunId,
    int ProductCountSent,
    int RecommendationsCount,
    int WarningCount,
    DateTime? ValidUntilUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> Warnings);
