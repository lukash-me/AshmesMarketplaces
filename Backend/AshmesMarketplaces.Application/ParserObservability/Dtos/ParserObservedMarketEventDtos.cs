namespace AshmesMarketplaces.Application.ParserObservability.Dtos;

public static class ParserObservedMarketEventTypes
{
    public const string StockDecreased = "stock_decreased";
    public const string StockIncreased = "stock_increased";
    public const string NewProductObserved = "new_product_observed";
    public const string ProductMissingInCurrent = "product_missing_in_current";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        StockDecreased,
        StockIncreased,
        NewProductObserved,
        ProductMissingInCurrent
    };
}

public sealed record ParserObservedMarketEventResponse(
    string? CurrentLogisticsRunId,
    string? PreviousLogisticsRunId,
    DateTime? CurrentObservedAtUtc,
    DateTime? PreviousObservedAtUtc,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<ParserObservedMarketEventItemDto> Items,
    ParserObservedMarketEventSummaryDto Summary,
    IReadOnlyList<string> Warnings);

public sealed record ParserObservedMarketEventItemDto(
    string EventType,
    Guid? ParserProductRowId,
    string WbProductId,
    string? WbRootId,
    string? Name,
    string? BrandName,
    string? SellerName,
    string? SourceCategory,
    string? SourceSubcategory,
    string Destination,
    int? PreviousQuantity,
    int? CurrentQuantity,
    int? QuantityChange,
    DateTime? PreviousObservedAtUtc,
    DateTime? CurrentObservedAtUtc,
    decimal? Price,
    decimal? PriceRegular,
    decimal? PriceDiscounted,
    decimal? PriceWbWallet,
    decimal? Rating,
    int? FeedbackCount,
    string? ImageUrl);

public sealed record ParserObservedMarketEventSummaryDto(
    int ComparedPairsCount,
    int StockDecreasedCount,
    int StockIncreasedCount,
    int NewProductObservedCount,
    int ProductMissingInCurrentCount,
    int UnchangedCount,
    int NotComparableQuantityCount,
    int CurrentOnlyProductsCount,
    int PreviousOnlyProductsCount,
    int OverlappingProductsCount,
    int TotalObservedDecrease,
    int TotalObservedIncrease);

public sealed class ParserObservedMarketEventQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? BrandName { get; init; }
    public string? SellerName { get; init; }
    public string? EventType { get; init; }
    public int? MinQuantityChange { get; init; }
    public string? CurrentLogisticsRunId { get; init; }
    public string? PreviousLogisticsRunId { get; init; }
}
