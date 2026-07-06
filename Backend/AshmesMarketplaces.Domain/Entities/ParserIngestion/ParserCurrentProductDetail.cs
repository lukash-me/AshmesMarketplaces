using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductDetail
{
    private ParserCurrentProductDetail() { }

    public ParserCurrentProductDetail(string wbProductId, string? wbRootId, string? sourceCategory, string? sourceSubcategory, string detailsHash, string detailsJson, DateTime observedAtUtc, string batchId)
    {
        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));
        Id = Guid.NewGuid();
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        DetailsHash = detailsHash;
        DetailsJson = detailsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string DetailsHash { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = "{}";
    public DateTime ObservedAtUtc { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string? wbRootId, string? sourceCategory, string? sourceSubcategory, string detailsHash, string detailsJson, DateTime observedAtUtc, string batchId)
    {
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        DetailsHash = detailsHash;
        DetailsJson = detailsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Restore(ParserCurrentSimpleSnapshot snapshot)
    {
        Update(
            snapshot.WbRootId,
            snapshot.SourceCategory,
            snapshot.SourceSubcategory,
            snapshot.Hash,
            snapshot.Json,
            snapshot.ObservedAtUtc,
            snapshot.BatchId);
    }
}
