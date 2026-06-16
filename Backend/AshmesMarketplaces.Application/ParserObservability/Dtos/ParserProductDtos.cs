using System.Text.Json;

namespace AshmesMarketplaces.Application.ParserObservability.Dtos;

public sealed record ParserProductListItemDto(
    Guid Id,
    string ParserRunId,
    DateTime ParsedAtUtc,
    string WbProductId,
    string? WbRootId,
    string Name,
    string? BrandName,
    string? SellerName,
    decimal? PriceRegular,
    decimal? PriceDiscounted,
    decimal? PriceWbWallet,
    int? DiscountPercent,
    int? TotalQuantity,
    int? RatingRounded,
    decimal? ReviewRating,
    int? FeedbackCount,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceQuery,
    string? ThumbnailUrl,
    ParserProductRankSummaryDto? Rank,
    ParserProductPositionDto? Position,
    ParserProductLogisticsSummaryDto? Logistics,
    ParserProductReviewEvidenceDto ParsedReviewEvidence);

public sealed record ParserProductDetailDto(
    Guid Id,
    string ParserRunId,
    DateTime ParsedAtUtc,
    string WbProductId,
    string? WbRootId,
    string Name,
    string? BrandName,
    string? SellerName,
    decimal? PriceRegular,
    decimal? PriceDiscounted,
    decimal? PriceWbWallet,
    int? DiscountPercent,
    int? RatingRounded,
    decimal? ReviewRating,
    int? FeedbackCount,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceQuery,
    string Marketplace,
    string? SkuProduct,
    string? Entity,
    long? BrandIdOnMp,
    long? SellerIdOnMp,
    int? TotalQuantity,
    string? FeedbackCountSource,
    IReadOnlyList<string> ImageUrls,
    int? ImageCount,
    long? SubjectParentId,
    long? SubjectId,
    string? SourceRegionDest,
    string SourceFileKind,
    string SourceFileSha256,
    long SourceLineNumber,
    string RowHash,
    ParserProductRankSummaryDto? Rank,
    ParserProductPositionDto? Position,
    ParserProductLogisticsSummaryDto? Logistics,
    ParserProductLogisticsDetailDto? LogisticsDetail,
    ParserProductDeliveryProfileDto? DeliveryProfile,
    string? Description,
    JsonElement? Characteristics,
    JsonElement? GroupedOptions,
    ParserProductVisualAnalysisDto? VisualAnalysis,
    ParserProductReviewEvidenceDto ParsedReviewEvidence);

public sealed record ParserProductVisualAnalysisDto(
    string State,
    IReadOnlyList<string> Facts);

public sealed record ParserProductRankSummaryDto(
    int AbsolutePosition,
    int Page,
    int PositionOnPage,
    string Query,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceRegionDest,
    string? Sort,
    DateTime ObservedAtUtc,
    string ParserRunId,
    string RankContextId,
    int ContextsCount);

public sealed record ParserProductPositionDto(
    string State,
    int? AbsolutePosition,
    int? ObservedRangeLimit,
    string? Query,
    string? SourceCategory,
    string? SourceSubcategory,
    DateTime? ObservedAtUtc);

public sealed record ParserProductReviewEvidenceDto(
    int RootFetchCount,
    int ParsedReviewCount,
    int ParsedReplyCount,
    string? LatestReviewRunId,
    string AttributionMode,
    bool IsRootScoped,
    bool IsFullHistoryUnknown,
    bool HasCappedRootPayload);

public sealed record ParserProductLogisticsSummaryDto(
    int? TotalQuantityObserved,
    bool? QuantityIsCapped,
    int? QuantityCapObserved,
    string TotalQuantityLabel,
    string? QuantitySemantics,
    int WarehouseCount,
    string? Destination,
    string? DeliveryProfileKey,
    string? DeliveryDestinationName,
    string? DeliveryProfileVersion,
    string? DeliveryDestinationCity,
    string? DeliveryDestinationLabel,
    string? DeliveryDestinationAddress,
    decimal? DeliveryDestinationLatitude,
    decimal? DeliveryDestinationLongitude,
    string? LatestLogisticsRunId,
    DateTime? ObservedAtUtc,
    bool HasWarehouseRows);

public sealed record ParserProductLogisticsDetailDto(
    ParserProductLogisticsSummaryDto Summary,
    string? ProductWhRaw,
    string? DeliveryProfileKey,
    string? DeliveryDestinationName,
    string? DeliveryProfileVersion,
    string? DeliveryDestinationCity,
    string? DeliveryDestinationLabel,
    string? DeliveryDestinationAddress,
    decimal? DeliveryDestinationLatitude,
    decimal? DeliveryDestinationLongitude,
    int? ProductTime1Raw,
    int? ProductTime2Raw,
    long? ProductDtypeRaw,
    int? ProductDistRaw,
    IReadOnlyList<ParserWarehouseAvailabilityDto> WarehouseRows);

public sealed record ParserProductDeliveryProfileDto(
    string? LatestLogisticsRunId,
    int DestinationCount,
    int WarehouseSourceCount,
    int? BestProductTime1Raw,
    int? WorstProductTime1Raw,
    int? ProductTime1SpreadRaw,
    ParserProductDeliveryLocationEstimateDto? LocationEstimate,
    IReadOnlyList<ParserProductDeliveryDestinationSignalDto> Destinations);

public sealed record ParserProductDeliveryLocationEstimateDto(
    string Status,
    string? ZoneKey,
    string? ZoneTitle,
    string? Confidence,
    string? NearestDestinationName,
    string? NearestDeliveryLabel,
    int? NearestDeliveryHours,
    string? SecondDestinationName,
    int? SecondDeliveryHours,
    string? FarthestDestinationName,
    int? DeliverySpreadHours,
    IReadOnlyList<string> Evidence);

public sealed record ParserProductDeliveryDestinationSignalDto(
    string Destination,
    string? DeliveryProfileKey,
    string? DeliveryDestinationName,
    string? DeliveryProfileVersion,
    string? DeliveryDestinationCity,
    string? DeliveryDestinationLabel,
    string? DeliveryDestinationAddress,
    decimal? DeliveryDestinationLatitude,
    decimal? DeliveryDestinationLongitude,
    int? TotalQuantityObserved,
    int WarehouseCount,
    int? ProductTime1Raw,
    int? ProductTime2Raw,
    int? ProductDistRaw,
    string? VisibleDeliveryStatus,
    string? VisibleDeliveryLabel,
    DateTime? VisibleDeliveryDate,
    string? VisibleDeliverySource,
    DateTime? VisibleDeliveryObservedAtUtc,
    JsonElement? VisibleDeliveryRawPayload,
    DateTime? ObservedAtUtc);

public sealed record ParserWarehouseAvailabilityDto(
    string? WarehouseIdOnMp,
    string? DeliveryProfileKey,
    string? DeliveryDestinationName,
    string? DeliveryProfileVersion,
    string? DeliveryDestinationCity,
    string? DeliveryDestinationLabel,
    string? DeliveryDestinationAddress,
    decimal? DeliveryDestinationLatitude,
    decimal? DeliveryDestinationLongitude,
    string? OptionId,
    string? SizeName,
    string? SizeOrigName,
    int? QuantityObserved,
    bool? QuantityIsCapped,
    int? QuantityCapObserved,
    string? QuantitySemantics,
    int? StockPriorityRaw,
    int? StockTime1Raw,
    int? StockTime2Raw,
    long? StockDtypeRaw,
    int? StockDistRaw,
    decimal? PriceBasic,
    decimal? PriceProduct,
    decimal? PriceLogisticsRaw,
    decimal? PriceReturnRaw);

public sealed record ParserProductLogisticsSummaryAggregateDto(
    string? ProductRunId,
    string? LogisticsRunId,
    string? SourceCategory,
    string? SourceSubcategory,
    int ProductsTotal,
    int ProductsWithLogistics,
    int ProductsWithoutLogistics,
    int ProductsWithQuantity,
    int ProductsWithoutQuantity,
    int? QuantityMin,
    int? QuantityMax,
    decimal? QuantityAverage,
    decimal? QuantityMedian,
    ParserProductQuantityBucketsDto QuantityBuckets,
    int ProductsWithWarehouseRows,
    int WarehouseRowsTotal,
    int DistinctWarehouseIds,
    decimal AverageWarehousesPerProduct,
    IReadOnlyList<ParserProductLogisticsDestinationSummaryDto> Destinations,
    DateTime? LatestObservedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record ParserProductQuantityBucketsDto(
    int Zero,
    int OneToFive,
    int SixToTwenty,
    int TwentyOneToThirtyNine,
    int FortyPlusOrHigh,
    int Unknown);

public sealed record ParserProductLogisticsDestinationSummaryDto(
    string Destination,
    string? DeliveryProfileKey,
    string? DeliveryDestinationName,
    string? DeliveryProfileVersion,
    string? DeliveryDestinationCity,
    string? DeliveryDestinationLabel,
    string? DeliveryDestinationAddress,
    decimal? DeliveryDestinationLatitude,
    decimal? DeliveryDestinationLongitude,
    int ProductsWithLogistics,
    DateTime? LatestObservedAtUtc);

public sealed record ParserProductFilterOptionsDto(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Subcategories,
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Sellers);

public sealed class ParserProductFilterOptionsQuery
{
    public string? ParserRunId { get; init; }
    public bool IncludeTestRuns { get; init; }
    public bool TestRunsOnly { get; init; }
    public string? TestLabel { get; init; }
    public string? Search { get; init; }
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? BrandName { get; init; }
    public string? SellerName { get; init; }
}

public sealed class ParserProductLogisticsSummaryQuery
{
    public string? ParserRunId { get; init; }
    public bool IncludeTestRuns { get; init; }
    public bool TestRunsOnly { get; init; }
    public string? TestLabel { get; init; }
    public string? Search { get; init; }
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? BrandName { get; init; }
    public string? SellerName { get; init; }
    public string? WbRootId { get; init; }
}

public sealed class ParserProductListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public string? ParserRunId { get; init; }
    public bool IncludeTestRuns { get; init; }
    public bool TestRunsOnly { get; init; }
    public string? TestLabel { get; init; }
    public bool RequireDeliveryProfile { get; init; }
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? BrandName { get; init; }
    public string? SellerName { get; init; }
    public string? WbRootId { get; init; }
    public decimal? PriceDiscountedFrom { get; init; }
    public decimal? PriceDiscountedTo { get; init; }
    public decimal? ReviewRatingFrom { get; init; }
    public decimal? ReviewRatingTo { get; init; }
    public int? FeedbackCountFrom { get; init; }
    public int? FeedbackCountTo { get; init; }
}
