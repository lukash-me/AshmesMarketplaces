namespace AshmesMarketplaces.Application.ParserObservability.Dtos;

public sealed record ParserObservedStockDecreaseResponse(
    string? CurrentLogisticsRunId,
    string? PreviousLogisticsRunId,
    DateTime? CurrentObservedAtUtc,
    DateTime? PreviousObservedAtUtc,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<ParserObservedStockDecreaseItemDto> Items,
    ParserObservedStockDecreaseSummaryDto Summary,
    IReadOnlyList<string> Warnings);

public sealed record ParserObservedStockDecreaseItemDto(
    string WbProductId,
    string? WbRootId,
    string? Name,
    string? BrandName,
    string? SellerName,
    string? SourceCategory,
    string? SourceSubcategory,
    string Destination,
    int PreviousQuantity,
    int CurrentQuantity,
    int QuantityDecrease,
    DateTime PreviousObservedAtUtc,
    DateTime CurrentObservedAtUtc,
    decimal? Price,
    decimal? Rating,
    int? FeedbackCount,
    string? ImageUrl);

public sealed record ParserObservedStockDecreaseSummaryDto(
    int ComparedProductsCount,
    int ProductsWithDecrease,
    int ProductsWithoutComparableQuantity,
    int CurrentOnlyProductsCount,
    int PreviousOnlyProductsCount,
    int NonOverlappingProductsCount,
    int TotalObservedDecrease,
    int? MaxObservedDecrease,
    decimal? AverageObservedDecrease);

public sealed class ParserObservedStockDecreaseQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public string? SourceCategory { get; init; }
    public string? SourceSubcategory { get; init; }
    public string? BrandName { get; init; }
    public string? SellerName { get; init; }
    public int? MinDecrease { get; init; }
    public string? CurrentLogisticsRunId { get; init; }
    public string? PreviousLogisticsRunId { get; init; }
}
