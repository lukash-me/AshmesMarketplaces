namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductRow
{
    private ParserCurrentProductRow() { }

    public ParserCurrentProductRow(ParserProductRow row, ProductGroupHashes hashes, DateTime updatedAtUtc)
    {
        Id = Guid.NewGuid();
        Apply(row, hashes, updatedAtUtc);
    }

    public Guid Id { get; private set; }
    public Guid ProductRowId { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public DateTime ParsedAtUtc { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string? SourceQuery { get; private set; }
    public string? SourceRegionDest { get; private set; }
    public long? BrandIdOnMp { get; private set; }
    public string? BrandName { get; private set; }
    public long? SellerIdOnMp { get; private set; }
    public string? SellerName { get; private set; }
    public decimal? PriceRegular { get; private set; }
    public decimal? PriceDiscounted { get; private set; }
    public decimal? PriceWbWallet { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public int? TotalQuantity { get; private set; }
    public decimal? RatingRounded { get; private set; }
    public decimal? ReviewRating { get; private set; }
    public int? FeedbackCount { get; private set; }
    public string? FeedbackCountSource { get; private set; }
    public int? ImageCount { get; private set; }
    public string? ImageUrlsJson { get; private set; }
    public string? PositionState { get; private set; }
    public int? PositionAbsolute { get; private set; }
    public int? PositionObservedRangeLimit { get; private set; }
    public string? PositionQuery { get; private set; }
    public DateTime? PositionObservedAtUtc { get; private set; }
    public string? IdentityHash { get; private set; }
    public string? PriceHash { get; private set; }
    public string? StockHash { get; private set; }
    public string? RatingHash { get; private set; }
    public string? ReviewsHash { get; private set; }
    public string? MediaHash { get; private set; }
    public string? SellerBrandHash { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Apply(ParserProductRow row, ProductGroupHashes hashes, DateTime updatedAtUtc)
    {
        ProductRowId = row.Id;
        ParserRunId = row.ParserRunId;
        ParsedAtUtc = row.ParsedAtUtc;
        WbProductId = row.WbProductId;
        WbRootId = row.WbRootId;
        Name = row.Name;
        SourceCategory = row.SourceCategory;
        SourceSubcategory = row.SourceSubcategory;
        SourceQuery = row.SourceQuery;
        SourceRegionDest = row.SourceRegionDest;
        BrandIdOnMp = row.BrandIdOnMp;
        BrandName = row.BrandName;
        SellerIdOnMp = row.SellerIdOnMp;
        SellerName = row.SellerName;
        PriceRegular = row.PriceRegular;
        PriceDiscounted = row.PriceDiscounted;
        PriceWbWallet = row.PriceWbWallet;
        DiscountPercent = row.DiscountPercent;
        TotalQuantity = row.TotalQuantity;
        RatingRounded = row.RatingRounded;
        ReviewRating = row.ReviewRating;
        FeedbackCount = row.FeedbackCount;
        FeedbackCountSource = row.FeedbackCountSource;
        ImageCount = row.ImageCount;
        ImageUrlsJson = row.ImageUrls?.RootElement.GetRawText();
        IdentityHash = hashes.IdentityHash;
        PriceHash = hashes.PriceHash;
        StockHash = hashes.StockHash;
        RatingHash = hashes.RatingHash;
        ReviewsHash = hashes.ReviewsHash;
        MediaHash = hashes.MediaHash;
        SellerBrandHash = hashes.SellerBrandHash;
        UpdatedAtUtc = updatedAtUtc;
    }
}

public sealed record ProductGroupHashes(
    string IdentityHash,
    string PriceHash,
    string StockHash,
    string RatingHash,
    string ReviewsHash,
    string MediaHash,
    string SellerBrandHash);
