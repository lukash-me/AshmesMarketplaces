using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProductRow : IDisposable
{
    private ParserProductRow() { }

    public ParserProductRow(
        Guid idParserRun,
        Guid idParserFile,
        long sourceLineNumber,
        string rowHash,
        int schemaVersion,
        string marketplace,
        string parserRunId,
        DateTime parsedAtUtc,
        string? sourceCategory,
        string? sourceSubcategory,
        string? sourceQuery,
        string? sourceRegionDest,
        string wbProductId,
        string? skuProduct,
        string name,
        string? entity,
        long? brandIdOnMp,
        string? brandName,
        long? sellerIdOnMp,
        string? sellerName,
        decimal? priceRegular,
        decimal? priceDiscounted,
        decimal? priceWbWallet,
        int? discountPercent,
        int? totalQuantity,
        int? ratingRounded,
        decimal? reviewRating,
        int? feedbackCount,
        string? feedbackCountSource,
        JsonDocument? imageUrls,
        int? imageCount,
        string? wbRootId,
        long? subjectParentId,
        long? subjectId,
        JsonDocument? rawObservedFields)
    {
        ParserStagingGuard.EnsureSource(idParserRun, idParserFile, sourceLineNumber, rowHash);

        if (string.IsNullOrWhiteSpace(marketplace))
            throw new ArgumentException("Marketplace is required.", nameof(marketplace));

        if (string.IsNullOrWhiteSpace(parserRunId))
            throw new ArgumentException("Parser run id is required.", nameof(parserRunId));

        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        DateTimeUtc.EnsureUtc(parsedAtUtc, nameof(parsedAtUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        SourceLineNumber = sourceLineNumber;
        RowHash = rowHash;
        SchemaVersion = schemaVersion;
        Marketplace = marketplace;
        ParserRunId = parserRunId;
        ParsedAtUtc = parsedAtUtc;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        SourceQuery = sourceQuery;
        SourceRegionDest = sourceRegionDest;
        WbProductId = wbProductId;
        SkuProduct = skuProduct;
        Name = name;
        Entity = entity;
        BrandIdOnMp = brandIdOnMp;
        BrandName = brandName;
        SellerIdOnMp = sellerIdOnMp;
        SellerName = sellerName;
        PriceRegular = priceRegular;
        PriceDiscounted = priceDiscounted;
        PriceWbWallet = priceWbWallet;
        DiscountPercent = discountPercent;
        TotalQuantity = totalQuantity;
        RatingRounded = ratingRounded;
        ReviewRating = reviewRating;
        FeedbackCount = feedbackCount;
        FeedbackCountSource = feedbackCountSource;
        ImageUrls = imageUrls;
        ImageCount = imageCount;
        WbRootId = wbRootId;
        SubjectParentId = subjectParentId;
        SubjectId = subjectId;
        RawObservedFields = rawObservedFields;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public Guid IdParserFile { get; private set; }
    public long SourceLineNumber { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string Marketplace { get; private set; } = string.Empty;
    public string ParserRunId { get; private set; } = string.Empty;
    public DateTime ParsedAtUtc { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string? SourceQuery { get; private set; }
    public string? SourceRegionDest { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? SkuProduct { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Entity { get; private set; }
    public long? BrandIdOnMp { get; private set; }
    public string? BrandName { get; private set; }
    public long? SellerIdOnMp { get; private set; }
    public string? SellerName { get; private set; }
    public decimal? PriceRegular { get; private set; }
    public decimal? PriceDiscounted { get; private set; }
    public decimal? PriceWbWallet { get; private set; }
    public int? DiscountPercent { get; private set; }
    public int? TotalQuantity { get; private set; }
    public int? RatingRounded { get; private set; }
    public decimal? ReviewRating { get; private set; }
    public int? FeedbackCount { get; private set; }
    public string? FeedbackCountSource { get; private set; }
    public JsonDocument? ImageUrls { get; private set; }
    public int? ImageCount { get; private set; }
    public string? WbRootId { get; private set; }
    public long? SubjectParentId { get; private set; }
    public long? SubjectId { get; private set; }
    public JsonDocument? RawObservedFields { get; private set; }

    public void Dispose()
    {
        ImageUrls?.Dispose();
        RawObservedFields?.Dispose();
    }
}
