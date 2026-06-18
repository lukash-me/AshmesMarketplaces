using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public sealed record MarketHotProductsSnapshot(
    string Marketplace,
    string? SourceCategory,
    IReadOnlyList<string> SourceSubcategories,
    string? ProductParserRunId,
    string? RankParserRunId,
    IReadOnlyList<string> ReviewParserRunIds,
    IReadOnlyList<MarketProductFeatureSnapshot> Products);

public sealed record HotProductsProductSelectionRow(
    Guid Id,
    string WbProductId,
    string ParserRunId,
    DateTime ParsedAtUtc,
    long SourceLineNumber);

public sealed record MarketProductFeatureSnapshot(
    Guid ParserProductRowId,
    MarketProductFeatureDto Product,
    string ProductName,
    string? BrandName,
    string? SellerName,
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
    int? TotalQuantity);
