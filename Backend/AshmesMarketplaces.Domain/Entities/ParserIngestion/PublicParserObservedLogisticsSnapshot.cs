using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class PublicParserObservedLogisticsSnapshot
{
    public const string SnapshotKeyValue = "default";
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private PublicParserObservedLogisticsSnapshot() { }

    public PublicParserObservedLogisticsSnapshot(DateTime createdAtUtc)
    {
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        SnapshotKey = SnapshotKeyValue;
        MarketEventsJson = "[]";
        MarketEventSummaryJson = "{}";
        StockDecreaseItemsJson = "[]";
        StockDecreaseSummaryJson = "{}";
        Status = FailedStatus;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string SnapshotKey { get; private set; } = SnapshotKeyValue;
    public string? CurrentLogisticsRunId { get; private set; }
    public string? PreviousLogisticsRunId { get; private set; }
    public DateTime? CurrentObservedAtUtc { get; private set; }
    public DateTime? PreviousObservedAtUtc { get; private set; }
    public string MarketEventsJson { get; private set; } = "[]";
    public string MarketEventSummaryJson { get; private set; } = "{}";
    public string StockDecreaseItemsJson { get; private set; } = "[]";
    public string StockDecreaseSummaryJson { get; private set; } = "{}";
    public int MarketEventsCount { get; private set; }
    public int StockDecreasesCount { get; private set; }
    public DateTime? CalculatedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public string Status { get; private set; } = FailedStatus;
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkCompleted(
        string? currentLogisticsRunId,
        string? previousLogisticsRunId,
        DateTime? currentObservedAtUtc,
        DateTime? previousObservedAtUtc,
        string marketEventsJson,
        string marketEventSummaryJson,
        string stockDecreaseItemsJson,
        string stockDecreaseSummaryJson,
        int marketEventsCount,
        int stockDecreasesCount,
        DateTime calculatedAtUtc,
        long durationMs)
    {
        if (currentObservedAtUtc.HasValue)
            DateTimeUtc.EnsureUtc(currentObservedAtUtc.Value, nameof(currentObservedAtUtc));
        if (previousObservedAtUtc.HasValue)
            DateTimeUtc.EnsureUtc(previousObservedAtUtc.Value, nameof(previousObservedAtUtc));
        DateTimeUtc.EnsureUtc(calculatedAtUtc, nameof(calculatedAtUtc));

        CurrentLogisticsRunId = currentLogisticsRunId;
        PreviousLogisticsRunId = previousLogisticsRunId;
        CurrentObservedAtUtc = currentObservedAtUtc;
        PreviousObservedAtUtc = previousObservedAtUtc;
        MarketEventsJson = string.IsNullOrWhiteSpace(marketEventsJson) ? "[]" : marketEventsJson;
        MarketEventSummaryJson = string.IsNullOrWhiteSpace(marketEventSummaryJson) ? "{}" : marketEventSummaryJson;
        StockDecreaseItemsJson = string.IsNullOrWhiteSpace(stockDecreaseItemsJson) ? "[]" : stockDecreaseItemsJson;
        StockDecreaseSummaryJson = string.IsNullOrWhiteSpace(stockDecreaseSummaryJson) ? "{}" : stockDecreaseSummaryJson;
        MarketEventsCount = marketEventsCount;
        StockDecreasesCount = stockDecreasesCount;
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
