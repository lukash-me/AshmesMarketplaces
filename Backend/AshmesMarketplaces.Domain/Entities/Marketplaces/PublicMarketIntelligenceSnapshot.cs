using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.Marketplaces;

public sealed class PublicMarketIntelligenceSnapshot
{
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private PublicMarketIntelligenceSnapshot() { }

    public PublicMarketIntelligenceSnapshot(
        string sourceCategory,
        string sourceSubcategory,
        string query,
        string sourceRegionDest,
        string sort,
        int topN,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query is required.", nameof(query));
        if (string.IsNullOrWhiteSpace(sourceRegionDest))
            throw new ArgumentException("Source region destination is required.", nameof(sourceRegionDest));
        if (string.IsNullOrWhiteSpace(sort))
            throw new ArgumentException("Sort is required.", nameof(sort));

        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        Query = query.Trim();
        SourceRegionDest = sourceRegionDest.Trim();
        Sort = sort.Trim();
        TopN = topN;
        PublicMarketIntelligenceJson = "{}";
        Status = FailedStatus;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string Query { get; private set; } = string.Empty;
    public string SourceRegionDest { get; private set; } = string.Empty;
    public string Sort { get; private set; } = string.Empty;
    public int TopN { get; private set; }
    public string PublicMarketIntelligenceJson { get; private set; } = "{}";
    public int SampleSize { get; private set; }
    public DateTime? LatestObservedAtUtc { get; private set; }
    public DateTime? CalculatedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public string Status { get; private set; } = FailedStatus;
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkCompleted(
        string publicMarketIntelligenceJson,
        int sampleSize,
        DateTime? latestObservedAtUtc,
        DateTime calculatedAtUtc,
        long durationMs)
    {
        if (string.IsNullOrWhiteSpace(publicMarketIntelligenceJson))
            throw new ArgumentException("Snapshot JSON is required.", nameof(publicMarketIntelligenceJson));

        if (latestObservedAtUtc.HasValue)
            DateTimeUtc.EnsureUtc(latestObservedAtUtc.Value, nameof(latestObservedAtUtc));
        DateTimeUtc.EnsureUtc(calculatedAtUtc, nameof(calculatedAtUtc));

        PublicMarketIntelligenceJson = publicMarketIntelligenceJson;
        SampleSize = sampleSize;
        LatestObservedAtUtc = latestObservedAtUtc;
        CalculatedAtUtc = calculatedAtUtc;
        DurationMs = durationMs;
        Status = CompletedStatus;
        Error = null;
        UpdatedAtUtc = calculatedAtUtc;
    }

    public void MarkFailed(DateTime failedAtUtc, string error)
    {
        DateTimeUtc.EnsureUtc(failedAtUtc, nameof(failedAtUtc));

        Status = FailedStatus;
        Error = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim();
        UpdatedAtUtc = failedAtUtc;
    }
}
