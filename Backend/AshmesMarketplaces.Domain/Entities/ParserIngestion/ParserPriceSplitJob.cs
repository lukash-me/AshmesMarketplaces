using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserPriceSplitJob
{
    private ParserPriceSplitJob() { }

    public ParserPriceSplitJob(
        string parserInstanceId,
        string proxyKey,
        string sourceCategory,
        string sourceSubcategory,
        int minPriceU,
        int maxPriceU,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            throw new ArgumentException("Parser instance id is required.", nameof(parserInstanceId));
        if (string.IsNullOrWhiteSpace(proxyKey))
            throw new ArgumentException("Proxy key is required.", nameof(proxyKey));
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));

        Id = Guid.NewGuid();
        ParserInstanceId = parserInstanceId.Trim();
        ProxyKey = proxyKey.Trim();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        Status = ParserPriceSplitJobStatuses.Active;
        MinPriceU = Math.Max(0, minPriceU);
        MaxPriceU = Math.Max(MinPriceU, maxPriceU);
        StartedAtUtc = nowUtc;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string ProxyKey { get; private set; } = string.Empty;
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int MinPriceU { get; private set; }
    public int MaxPriceU { get; private set; }
    public int TotalRangesCount { get; private set; }
    public int CompletedRangesCount { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateCounters(int totalRangesCount, int completedRangesCount, DateTime nowUtc)
    {
        DateTimeUtc.EnsureUtc(nowUtc, nameof(nowUtc));
        TotalRangesCount = Math.Max(0, totalRangesCount);
        CompletedRangesCount = Math.Max(0, completedRangesCount);
        UpdatedAtUtc = nowUtc;
    }
}
