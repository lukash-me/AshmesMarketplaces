using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class PublicProductAvailabilitySnapshot
{
    public const string SnapshotKeyValue = "default";
    public const string CompletedStatus = "completed";
    public const string FailedStatus = "failed";

    private PublicProductAvailabilitySnapshot() { }

    public PublicProductAvailabilitySnapshot(DateTime createdAtUtc)
    {
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        SnapshotKey = SnapshotKeyValue;
        ItemsJson = "[]";
        Status = FailedStatus;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string SnapshotKey { get; private set; } = SnapshotKeyValue;
    public string ItemsJson { get; private set; } = "[]";
    public int TotalCount { get; private set; }
    public DateTime? CalculatedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public string Status { get; private set; } = FailedStatus;
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkCompleted(string itemsJson, int totalCount, DateTime calculatedAtUtc, long durationMs)
    {
        DateTimeUtc.EnsureUtc(calculatedAtUtc, nameof(calculatedAtUtc));

        ItemsJson = string.IsNullOrWhiteSpace(itemsJson) ? "[]" : itemsJson;
        TotalCount = totalCount;
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
