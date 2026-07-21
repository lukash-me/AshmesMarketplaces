using System.Text.Json;

namespace AshmesMarketplaces.Application.MarketRecommendations.Intelligence;

public sealed record WorkspaceProductAnalysisIntelligenceRequest(
    string RequestId,
    DateTime GeneratedAtUtc,
    string Marketplace,
    MarketProductFeatureDto Product,
    WorkspaceProductHistoryDto History,
    IReadOnlyList<MarketProductFeatureDto> Candidates,
    WorkspaceProductAnalysisOptions Options);

public sealed record WorkspaceProductHistoryDto(
    IReadOnlyList<WorkspaceProductHistoryPointDto> PriceObservations,
    IReadOnlyList<WorkspaceProductHistoryPointDto> PositionObservations,
    IReadOnlyList<WorkspaceProductHistoryPointDto> StockObservations,
    IReadOnlyList<WorkspaceProductHistoryPointDto> FeedbackObservations);

public sealed record WorkspaceProductHistoryPointDto(
    DateTime ObservedAtUtc,
    decimal? Value);

public sealed record WorkspaceProductAnalysisOptions(
    int MaxSimilarProducts,
    string? Algorithm);

public sealed record WorkspaceProductAnalysisIntelligenceResponse(
    string RequestId,
    string Status,
    string Algorithm,
    string AlgorithmVersion,
    string ModelVersion,
    DateTime ComputedAtUtc,
    IReadOnlyList<WorkspaceProductSignalDto> Signals,
    IReadOnlyList<WorkspaceSimilarProductDto> SimilarProducts,
    IReadOnlyList<WorkspaceSimilarProductGroupDto> SimilarProductGroups,
    IReadOnlyList<string> Warnings);

public sealed record WorkspaceProductSignalDto(
    string Code,
    string Severity,
    string Title,
    string Description,
    IReadOnlyList<string> MetricFacts,
    decimal Confidence,
    JsonElement? Value = null);

public sealed record WorkspaceSimilarProductDto(
    string ProductKey,
    string? WbProductId,
    string? WbRootId,
    decimal SimilarityScore,
    string Reason);

public sealed record WorkspaceSimilarProductGroupDto(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<WorkspaceSimilarProductGroupItemDto> Items);

public sealed record WorkspaceSimilarProductGroupItemDto(
    string ProductKey,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Tags);
