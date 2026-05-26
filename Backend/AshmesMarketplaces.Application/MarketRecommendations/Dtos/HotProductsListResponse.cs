using System.Text.Json;

namespace AshmesMarketplaces.Application.MarketRecommendations.Dtos;

public sealed record HotProductsListResponse(
    HotProductsRunSummaryDto? Run,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<HotProductRecommendationListItemDto> Items);

public sealed record HotProductsRunSummaryDto(
    Guid RunId,
    DateTime ComputedAtUtc,
    DateTime? ValidUntilUtc,
    string Algorithm,
    string AlgorithmVersion,
    int ItemsTotal,
    int WarningCount);

public sealed record HotProductRecommendationListItemDto(
    Guid Id,
    int RankOrder,
    string ProductName,
    string? ThumbnailUrl,
    string? WbProductId,
    string? WbRootId,
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
    decimal Score,
    decimal Confidence,
    string Title,
    string Reason,
    IReadOnlyList<HotProductRecommendationFactorDto> Factors,
    DateTime? ValidUntilUtc);

public sealed record HotProductRecommendationFactorDto(
    string Code,
    string Label,
    JsonElement? Value,
    decimal Weight,
    string Direction);
