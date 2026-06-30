namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductReviewsSummary
{
    private ParserCurrentProductReviewsSummary() { }

    public ParserCurrentProductReviewsSummary(
        string wbProductId,
        string? wbRootId,
        string? sourceCategory,
        string? sourceSubcategory,
        int reviewsCount,
        decimal? averageRating,
        int recentNegativeCount,
        DateTime? lastReviewDateUtc,
        string reviewsHash,
        string reviewsJson,
        DateTime observedAtUtc,
        string batchId)
    {
        Id = Guid.NewGuid();
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        ReviewsCount = reviewsCount;
        AverageRating = averageRating;
        RecentNegativeCount = recentNegativeCount;
        LastReviewDateUtc = lastReviewDateUtc;
        ReviewsHash = reviewsHash;
        ReviewsJson = reviewsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public int ReviewsCount { get; private set; }
    public decimal? AverageRating { get; private set; }
    public int RecentNegativeCount { get; private set; }
    public DateTime? LastReviewDateUtc { get; private set; }
    public string ReviewsHash { get; private set; } = string.Empty;
    public string ReviewsJson { get; private set; } = "{}";
    public DateTime ObservedAtUtc { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(int reviewsCount, decimal? averageRating, int recentNegativeCount, DateTime? lastReviewDateUtc, string reviewsHash, string reviewsJson, DateTime observedAtUtc, string batchId)
    {
        ReviewsCount = reviewsCount;
        AverageRating = averageRating;
        RecentNegativeCount = recentNegativeCount;
        LastReviewDateUtc = lastReviewDateUtc;
        ReviewsHash = reviewsHash;
        ReviewsJson = reviewsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
