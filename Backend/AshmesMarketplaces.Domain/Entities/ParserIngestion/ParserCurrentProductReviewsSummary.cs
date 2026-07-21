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
        : this(
            wbProductId,
            wbRootId,
            sourceCategory,
            sourceSubcategory,
            reviewsCount,
            averageRating,
            recentNegativeCount,
            lastReviewDateUtc,
            null,
            reviewsCount,
            null,
            "unknown",
            "root_capped_fallback",
            null,
            reviewsHash,
            reviewsJson,
            observedAtUtc,
            batchId)
    {
    }

    public ParserCurrentProductReviewsSummary(
        string wbProductId,
        string? wbRootId,
        string? sourceCategory,
        string? sourceSubcategory,
        int reviewsCount,
        decimal? averageRating,
        int recentNegativeCount,
        DateTime? lastReviewDateUtc,
        int? marketplaceFeedbackCount,
        int fetchedReviewsCount,
        DateTime? oldestReviewDateUtc,
        string coverageStatus,
        string coverageSource,
        string? lastCoverageError,
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
        MarketplaceFeedbackCount = marketplaceFeedbackCount;
        FetchedReviewsCount = fetchedReviewsCount;
        OldestReviewDateUtc = oldestReviewDateUtc;
        CoverageStatus = coverageStatus;
        CoverageSource = coverageSource;
        LastCoverageError = lastCoverageError;
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
    public int? MarketplaceFeedbackCount { get; private set; }
    public int FetchedReviewsCount { get; private set; }
    public DateTime? OldestReviewDateUtc { get; private set; }
    public string CoverageStatus { get; private set; } = "unknown";
    public string CoverageSource { get; private set; } = "root_capped_fallback";
    public string? LastCoverageError { get; private set; }
    public string ReviewsHash { get; private set; } = string.Empty;
    public string ReviewsJson { get; private set; } = "{}";
    public DateTime ObservedAtUtc { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(int reviewsCount, decimal? averageRating, int recentNegativeCount, DateTime? lastReviewDateUtc, string reviewsHash, string reviewsJson, DateTime observedAtUtc, string batchId)
    {
        Update(
            reviewsCount,
            averageRating,
            recentNegativeCount,
            lastReviewDateUtc,
            MarketplaceFeedbackCount,
            reviewsCount,
            OldestReviewDateUtc,
            CoverageStatus,
            CoverageSource,
            LastCoverageError,
            reviewsHash,
            reviewsJson,
            observedAtUtc,
            batchId);
    }

    public void Update(int reviewsCount, decimal? averageRating, int recentNegativeCount, DateTime? lastReviewDateUtc, int? marketplaceFeedbackCount, int fetchedReviewsCount, DateTime? oldestReviewDateUtc, string coverageStatus, string coverageSource, string? lastCoverageError, string reviewsHash, string reviewsJson, DateTime observedAtUtc, string batchId)
    {
        ReviewsCount = reviewsCount;
        AverageRating = averageRating;
        RecentNegativeCount = recentNegativeCount;
        LastReviewDateUtc = lastReviewDateUtc;
        MarketplaceFeedbackCount = marketplaceFeedbackCount;
        FetchedReviewsCount = fetchedReviewsCount;
        OldestReviewDateUtc = oldestReviewDateUtc;
        CoverageStatus = coverageStatus;
        CoverageSource = coverageSource;
        LastCoverageError = lastCoverageError;
        ReviewsHash = reviewsHash;
        ReviewsJson = reviewsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Restore(ParserCurrentReviewsSummarySnapshot snapshot)
    {
        WbRootId = snapshot.WbRootId;
        SourceCategory = snapshot.SourceCategory;
        SourceSubcategory = snapshot.SourceSubcategory;
        Update(
            snapshot.ReviewsCount,
            snapshot.AverageRating,
            snapshot.RecentNegativeCount,
            snapshot.LastReviewDateUtc,
            snapshot.MarketplaceFeedbackCount,
            snapshot.FetchedReviewsCount,
            snapshot.OldestReviewDateUtc,
            snapshot.CoverageStatus,
            snapshot.CoverageSource,
            snapshot.LastCoverageError,
            snapshot.ReviewsHash,
            snapshot.ReviewsJson,
            snapshot.ObservedAtUtc,
            snapshot.BatchId);
    }
}
