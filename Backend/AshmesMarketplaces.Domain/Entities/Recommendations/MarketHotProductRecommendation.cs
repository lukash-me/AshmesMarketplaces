using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Recommendations;

public sealed class MarketHotProductRecommendation : IDisposable
{
    private MarketHotProductRecommendation() { }

    public MarketHotProductRecommendation(
        Guid idMarketRecommendationRun,
        string recommendationKey,
        string productKey,
        string? wbProductId,
        string? wbRootId,
        Guid? idParserProductRow,
        string? sourceCategory,
        string? sourceSubcategory,
        string productName,
        string? brandName,
        string? sellerName,
        decimal? priceSnapshot,
        decimal? priceWithoutDiscountSnapshot,
        decimal? walletPriceSnapshot,
        decimal? ratingSnapshot,
        int? feedbackCountSnapshot,
        int? parsedReviewCountSnapshot,
        int? parsedReplyCountSnapshot,
        int? positionSnapshot,
        string? positionState,
        int? observedRangeLimit,
        int? totalQuantitySnapshot,
        decimal score,
        decimal confidence,
        string title,
        string reason,
        string inputSnapshotHash,
        DateTime validUntilUtc,
        int rankOrder,
        JsonDocument factors,
        DateTime createdAtUtc)
    {
        if (idMarketRecommendationRun == Guid.Empty)
            throw new ArgumentException("Market recommendation run id is required.", nameof(idMarketRecommendationRun));

        if (string.IsNullOrWhiteSpace(recommendationKey))
            throw new ArgumentException("Recommendation key is required.", nameof(recommendationKey));

        if (string.IsNullOrWhiteSpace(productKey))
            throw new ArgumentException("Product key is required.", nameof(productKey));

        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));

        if (score is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");

        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (string.IsNullOrWhiteSpace(inputSnapshotHash))
            throw new ArgumentException("Input snapshot hash is required.", nameof(inputSnapshotHash));

        if (rankOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(rankOrder), "Rank order must be positive.");

        DateTimeUtc.EnsureUtc(validUntilUtc, nameof(validUntilUtc));
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        IdMarketRecommendationRun = idMarketRecommendationRun;
        RecommendationKey = recommendationKey;
        ProductKey = productKey;
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        IdParserProductRow = idParserProductRow;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        ProductName = productName;
        BrandName = brandName;
        SellerName = sellerName;
        PriceSnapshot = priceSnapshot;
        PriceWithoutDiscountSnapshot = priceWithoutDiscountSnapshot;
        WalletPriceSnapshot = walletPriceSnapshot;
        RatingSnapshot = ratingSnapshot;
        FeedbackCountSnapshot = feedbackCountSnapshot;
        ParsedReviewCountSnapshot = parsedReviewCountSnapshot;
        ParsedReplyCountSnapshot = parsedReplyCountSnapshot;
        PositionSnapshot = positionSnapshot;
        PositionState = positionState;
        ObservedRangeLimit = observedRangeLimit;
        TotalQuantitySnapshot = totalQuantitySnapshot;
        Score = score;
        Confidence = confidence;
        Title = title;
        Reason = reason;
        InputSnapshotHash = inputSnapshotHash;
        ValidUntilUtc = validUntilUtc;
        RankOrder = rankOrder;
        Factors = factors ?? throw new ArgumentNullException(nameof(factors));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdMarketRecommendationRun { get; private set; }
    public string RecommendationKey { get; private set; } = string.Empty;
    public string ProductKey { get; private set; } = string.Empty;
    public string? WbProductId { get; private set; }
    public string? WbRootId { get; private set; }
    public Guid? IdParserProductRow { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? BrandName { get; private set; }
    public string? SellerName { get; private set; }
    public decimal? PriceSnapshot { get; private set; }
    public decimal? PriceWithoutDiscountSnapshot { get; private set; }
    public decimal? WalletPriceSnapshot { get; private set; }
    public decimal? RatingSnapshot { get; private set; }
    public int? FeedbackCountSnapshot { get; private set; }
    public int? ParsedReviewCountSnapshot { get; private set; }
    public int? ParsedReplyCountSnapshot { get; private set; }
    public int? PositionSnapshot { get; private set; }
    public string? PositionState { get; private set; }
    public int? ObservedRangeLimit { get; private set; }
    public int? TotalQuantitySnapshot { get; private set; }
    public decimal Score { get; private set; }
    public decimal Confidence { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string InputSnapshotHash { get; private set; } = string.Empty;
    public DateTime ValidUntilUtc { get; private set; }
    public int RankOrder { get; private set; }
    public JsonDocument Factors { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public void Dispose()
    {
        Factors?.Dispose();
    }
}
