namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductRow
{
    private ParserCurrentProductRow() { }

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
    public string? BrandName { get; private set; }
    public string? SellerName { get; private set; }
    public decimal? PriceRegular { get; private set; }
    public decimal? PriceDiscounted { get; private set; }
    public decimal? PriceWbWallet { get; private set; }
    public int? TotalQuantity { get; private set; }
    public decimal? ReviewRating { get; private set; }
    public int? FeedbackCount { get; private set; }
    public string? PositionState { get; private set; }
    public int? PositionAbsolute { get; private set; }
    public int? PositionObservedRangeLimit { get; private set; }
    public string? PositionQuery { get; private set; }
    public DateTime? PositionObservedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}
