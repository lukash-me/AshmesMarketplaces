using System.Text.Json;

namespace AshmesMarketplaces.Application.MarketRecommendations.Intelligence;

public sealed record HotProductsIntelligenceRequest(
    string RequestId,
    DateTime GeneratedAtUtc,
    string Marketplace,
    HotProductsIntelligenceScope Scope,
    IReadOnlyList<MarketProductFeatureDto> Products,
    HotProductsIntelligenceOptions Options);

public sealed record HotProductsIntelligenceScope(
    string? SourceCategory,
    IReadOnlyList<string> SourceSubcategories,
    string? ParserRunId,
    string? RankRunId,
    IReadOnlyList<string> ReviewRunIds);

public sealed record HotProductsIntelligenceOptions(
    int? MaxRecommendations,
    decimal? MinConfidence,
    int? MinProductsForScoring,
    string? Algorithm);

public sealed record MarketProductDeliveryProfileDto(
    IReadOnlyList<MarketProductDeliveryDestinationDto> Destinations);

public sealed record MarketProductDeliveryDestinationDto(
    string? RegionKey,
    string? RegionName,
    string? DestinationCity,
    string? DestinationAddress,
    string? VisibleDeliveryLabel,
    DateTime? VisibleDeliveryDate,
    int? DeliveryHours,
    string? DeliverySourceType,
    int? TotalQuantityObserved,
    DateTime? ObservedAtUtc);

public sealed record MarketProductFeatureDto(
    string ProductKey,
    string? WbProductId,
    string? WbRootId,
    string? Name,
    string? BrandName,
    string? SellerName,
    string? SourceCategory,
    string? SourceSubcategory,
    decimal? Price,
    decimal? PriceWithoutDiscount,
    decimal? WalletPrice,
    decimal? Rating,
    int? FeedbackCount,
    int? ParsedReviewCount,
    int? ParsedReplyCount,
    int? Position,
    string? PositionState,
    int? ObservedRangeLimit,
    int? TotalQuantity,
    DateTime? SnapshotAtUtc,
    string? Description,
    JsonElement? Characteristics,
    int? ImageCount,
    MarketProductReviewSignalDto? ReviewSignals,
    MarketProductDeliveryProfileDto? DeliveryProfile = null,
    string? SourceType = null,
    decimal? CostPrice = null,
    string? SupplierName = null,
    string? SupplierUrl = null,
    int? PositiveReviewCount = null,
    int? ReviewSampleSize = null);

public sealed record MarketProductReviewSignalDto(
    int ParsedReviewCount,
    int ParsedReplyCount,
    int RatedReviewCount,
    decimal? AverageRating,
    int LowRatingReviewCount,
    int NegativeTextReviewCount,
    int BadReviewCount,
    int ReviewWindowSize,
    int RecentTwoWeeksCount,
    string? LatestReviewRunId,
    int SentimentVersion,
    string ReviewScope,
    IReadOnlyList<ReviewNegativeEvidenceDto> NegativeReviewEvidence);

public sealed record ReviewNegativeEvidenceDto(
    string? ReviewIdOnMp,
    string? SourceWbProductId,
    int? Rating,
    DateTime? CreatedAtOnMp,
    string Snippet,
    IReadOnlyList<string> ReasonCodes,
    decimal Score);

public sealed record HotProductsIntelligenceResponse(
    string RequestId,
    string Status,
    string Algorithm,
    string AlgorithmVersion,
    string ModelVersion,
    DateTime ComputedAtUtc,
    IReadOnlyList<HotProductRecommendationDto> Recommendations,
    IReadOnlyList<string> Warnings,
    JsonElement? Diagnostics = null);

public sealed record HotProductRecommendationDto(
    string RecommendationKey,
    string ProductKey,
    string? WbProductId,
    string? WbRootId,
    decimal Score,
    decimal Confidence,
    string Title,
    string Reason,
    IReadOnlyList<RecommendationFactorDto> Factors,
    string InputSnapshotHash,
    DateTime? ValidUntilUtc,
    JsonElement? Debug);

public sealed record RecommendationFactorDto(
    string Code,
    string Label,
    JsonElement? Value,
    decimal Weight,
    string Direction);

public sealed record IntelligenceErrorResponse(
    string? ErrorCode,
    string? Message,
    JsonElement? Details,
    string? RequestId,
    string? TraceId);
