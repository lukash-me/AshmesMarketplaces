namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed record ParserCurrentProductSnapshot(
    Guid ProductRowId,
    string ParserRunId,
    DateTime ParsedAtUtc,
    string WbProductId,
    string? WbRootId,
    string Name,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceQuery,
    string? SourceRegionDest,
    long? BrandIdOnMp,
    string? BrandName,
    long? SellerIdOnMp,
    string? SellerName,
    decimal? PriceRegular,
    decimal? PriceDiscounted,
    decimal? PriceWbWallet,
    decimal? DiscountPercent,
    int? TotalQuantity,
    decimal? RatingRounded,
    decimal? ReviewRating,
    int? FeedbackCount,
    string? FeedbackCountSource,
    int? ImageCount,
    string? ImageUrlsJson,
    string? PositionState,
    int? PositionAbsolute,
    int? PositionObservedRangeLimit,
    string? PositionQuery,
    DateTime? PositionObservedAtUtc,
    string? IdentityHash,
    string? PriceHash,
    string? StockHash,
    string? RatingHash,
    string? ReviewsHash,
    string? MediaHash,
    string? SellerBrandHash);

public sealed record ParserCurrentSimpleSnapshot(
    string WbProductId,
    string? WbRootId,
    string? SourceCategory,
    string? SourceSubcategory,
    string Hash,
    string Json,
    DateTime ObservedAtUtc,
    string BatchId);

public sealed record ParserCurrentReviewsSummarySnapshot(
    string WbProductId,
    string? WbRootId,
    string? SourceCategory,
    string? SourceSubcategory,
    int ReviewsCount,
    decimal? AverageRating,
    int RecentNegativeCount,
    DateTime? LastReviewDateUtc,
    int? MarketplaceFeedbackCount,
    int FetchedReviewsCount,
    DateTime? OldestReviewDateUtc,
    string CoverageStatus,
    string CoverageSource,
    string? LastCoverageError,
    string ReviewsHash,
    string ReviewsJson,
    DateTime ObservedAtUtc,
    string BatchId);

public sealed record ParserCurrentReviewEvidenceSnapshot(
    string WbProductId,
    string? WbRootId,
    string ReviewIdOnMp,
    string ReviewHash,
    string ReviewJson,
    int? Rating,
    DateTime? CreatedAtOnMp,
    DateTime ObservedAtUtc,
    string BatchId);

public static class ParserRunCurrentEntitySnapshots
{
    public static ParserCurrentProductSnapshot FromProduct(ParserCurrentProductRow row) =>
        new(
            row.ProductRowId,
            row.ParserRunId,
            row.ParsedAtUtc,
            row.WbProductId,
            row.WbRootId,
            row.Name,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            row.SourceRegionDest,
            row.BrandIdOnMp,
            row.BrandName,
            row.SellerIdOnMp,
            row.SellerName,
            row.PriceRegular,
            row.PriceDiscounted,
            row.PriceWbWallet,
            row.DiscountPercent,
            row.TotalQuantity,
            row.RatingRounded,
            row.ReviewRating,
            row.FeedbackCount,
            row.FeedbackCountSource,
            row.ImageCount,
            row.ImageUrlsJson,
            row.PositionState,
            row.PositionAbsolute,
            row.PositionObservedRangeLimit,
            row.PositionQuery,
            row.PositionObservedAtUtc,
            row.IdentityHash,
            row.PriceHash,
            row.StockHash,
            row.RatingHash,
            row.ReviewsHash,
            row.MediaHash,
            row.SellerBrandHash);

    public static ParserCurrentSimpleSnapshot FromLogistics(ParserCurrentProductLogistics row) =>
        new(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, row.LogisticsHash, row.LogisticsJson, row.ObservedAtUtc, row.BatchId);

    public static ParserCurrentSimpleSnapshot FromDetails(ParserCurrentProductDetail row) =>
        new(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, row.DetailsHash, row.DetailsJson, row.ObservedAtUtc, row.BatchId);

    public static ParserCurrentSimpleSnapshot FromRank(ParserCurrentProductRank row) =>
        new(row.WbProductId, row.WbRootId, row.SourceCategory, row.SourceSubcategory, row.RankHash, row.RankJson, row.ObservedAtUtc, row.BatchId);

    public static ParserCurrentReviewsSummarySnapshot FromReviewsSummary(ParserCurrentProductReviewsSummary row) =>
        new(
            row.WbProductId,
            row.WbRootId,
            row.SourceCategory,
            row.SourceSubcategory,
            row.ReviewsCount,
            row.AverageRating,
            row.RecentNegativeCount,
            row.LastReviewDateUtc,
            row.MarketplaceFeedbackCount,
            row.FetchedReviewsCount,
            row.OldestReviewDateUtc,
            row.CoverageStatus,
            row.CoverageSource,
            row.LastCoverageError,
            row.ReviewsHash,
            row.ReviewsJson,
            row.ObservedAtUtc,
            row.BatchId);

    public static ParserCurrentReviewEvidenceSnapshot FromReviewEvidence(ParserCurrentProductReviewEvidence row) =>
        new(
            row.WbProductId,
            row.WbRootId,
            row.ReviewIdOnMp,
            row.ReviewHash,
            row.ReviewJson,
            row.Rating,
            row.CreatedAtOnMp,
            row.ObservedAtUtc,
            row.BatchId);
}
