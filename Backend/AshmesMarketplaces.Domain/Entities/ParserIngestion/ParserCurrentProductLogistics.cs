using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductLogistics
{
    private ParserCurrentProductLogistics() { }

    public ParserCurrentProductLogistics(
        string wbProductId,
        string? wbRootId,
        string? sourceCategory,
        string? sourceSubcategory,
        string logisticsHash,
        string logisticsJson,
        DateTime observedAtUtc,
        string batchId)
    {
        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));
        Id = Guid.NewGuid();
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        LogisticsHash = logisticsHash;
        LogisticsJson = logisticsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string LogisticsHash { get; private set; } = string.Empty;
    public string LogisticsJson { get; private set; } = "{}";
    public DateTime ObservedAtUtc { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string? wbRootId, string? sourceCategory, string? sourceSubcategory, string logisticsHash, string logisticsJson, DateTime observedAtUtc, string batchId)
    {
        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        LogisticsHash = logisticsHash;
        LogisticsJson = logisticsJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

}
