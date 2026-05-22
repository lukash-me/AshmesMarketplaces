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
    int? RatingRounded,
    decimal? ReviewRating,
    int? FeedbackCount,
    string? SourceCategory,
    string? SourceSubcategory,
    string? SourceQuery,
    string? ThumbnailUrl);

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
    string RowHash);

public sealed class ParserProductListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public string? ParserRunId { get; init; }
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
