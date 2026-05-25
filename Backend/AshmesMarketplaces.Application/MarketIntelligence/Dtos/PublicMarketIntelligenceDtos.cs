namespace AshmesMarketplaces.Application.MarketIntelligence.Dtos;

public sealed class PublicMarketIntelligenceQuery
{
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? Query { get; init; }
    public string? SourceRegionDest { get; init; }
    public string? Sort { get; init; }
    public int TopN { get; init; } = 100;
    public string? LatestRankRunId { get; init; }
    public string? BaselineRankRunId { get; init; }
    public string? LatestProductRunId { get; init; }
    public string? BaselineProductRunId { get; init; }
}

public sealed record PublicMarketIntelligenceDto(
    PublicMarketContextDto Context,
    ObservationWindowDto ObservationWindow,
    IReadOnlyList<MarketEventDto> Events,
    IReadOnlyList<CompetitorWeaknessDto> CompetitorWeaknesses,
    PublicMarketPromoPressureDto PromoPressure,
    PublicMarketPricePressureDto PricePressure,
    PublicMarketStockPressureDto StockPressure,
    ConcentrationSummaryDto Concentration,
    IReadOnlyList<string> Limitations);

public sealed record PublicMarketContextDto(
    string Marketplace,
    string? SourceCategory,
    string? SourceSubcategory,
    string Query,
    string? SourceRegionDest,
    string? Sort,
    int TopN);

public sealed record ObservationWindowDto(
    string? LatestRankRunId,
    string? BaselineRankRunId,
    string? LatestProductRunId,
    string? BaselineProductRunId,
    DateTime? LatestObservedAtUtc,
    DateTime? BaselineObservedAtUtc,
    bool IsComparable,
    string CoverageStatus,
    IReadOnlyList<string> Limitations);

public sealed record MarketEventDto(
    string Type,
    string Group,
    string Severity,
    string Title,
    string Description,
    string WbProductId,
    string? WbRootId,
    string? ProductName,
    string? BrandName,
    string? SellerName,
    string? ProductRowId,
    string? ThumbnailUrl,
    string? BeforeValue,
    string? AfterValue,
    DateTime ObservedAtUtc,
    decimal Confidence,
    IReadOnlyList<string> Limitations);

public sealed record CompetitorWeaknessDto(
    string Type,
    string Severity,
    string Title,
    string Description,
    string WbProductId,
    string? WbRootId,
    string? ProductName,
    string? BrandName,
    string? SellerName,
    string? ProductRowId,
    string? ThumbnailUrl,
    int Position,
    string? MetricValue,
    string? ReferenceValue,
    string Explanation,
    IReadOnlyList<string> Limitations);

public sealed record PressureSummaryDto(
    string Type,
    string Title,
    string Description,
    decimal? CurrentValue,
    decimal? BaselineValue,
    decimal? Delta,
    string Unit,
    IReadOnlyList<string> Limitations);

public sealed record PublicMarketPromoPressureDto(
    IReadOnlyList<PressureSummaryDto> Summaries,
    IReadOnlyList<string> Limitations);

public sealed record PublicMarketPricePressureDto(
    IReadOnlyList<PressureSummaryDto> Summaries,
    IReadOnlyList<HighPriceVisibleProductDto> HighPriceVisibleProducts,
    IReadOnlyList<string> Limitations);

public sealed record HighPriceVisibleProductDto(
    string WbProductId,
    string? WbRootId,
    string? ProductName,
    string? BrandName,
    string? SellerName,
    string? ProductRowId,
    string? ThumbnailUrl,
    int Position,
    decimal? CurrentPrice,
    decimal? ReferencePrice,
    string Explanation,
    IReadOnlyList<string> Limitations);

public sealed record PublicMarketStockPressureDto(
    int ExactZeroCount,
    int ExactLowStockCount,
    int CappedCount,
    int UnknownCount,
    IReadOnlyList<StockStatusSummaryDto> StatusBreakdown,
    IReadOnlyList<HighRankLowStockProductDto> HighRankLowStockProducts,
    IReadOnlyList<string> Limitations);

public sealed record StockStatusSummaryDto(
    string Status,
    int Count,
    StockValueDto Example);

public sealed record HighRankLowStockProductDto(
    string WbProductId,
    string? WbRootId,
    string? ProductName,
    string? BrandName,
    string? SellerName,
    string? ProductRowId,
    string? ThumbnailUrl,
    int Position,
    StockValueDto Stock,
    string Explanation,
    IReadOnlyList<string> Limitations);

public sealed record StockValueDto(
    string Status,
    int? Value,
    string DisplayValue);

public sealed record ConcentrationSummaryDto(
    IReadOnlyList<ConcentrationLeaderDto> SellerLeaders,
    IReadOnlyList<ConcentrationLeaderDto> BrandLeaders,
    IReadOnlyList<RootClusterDto> RootClusters,
    IReadOnlyList<string> Limitations);

public sealed record ConcentrationLeaderDto(
    string Name,
    int SlotsCount,
    decimal SharePercent,
    string Description);

public sealed record RootClusterDto(
    string WbRootId,
    int ProductCount,
    int BestPosition,
    string Description);
