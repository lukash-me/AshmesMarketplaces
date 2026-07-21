namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductRank
{
    private ParserCurrentProductRank() { }

    public ParserCurrentProductRank(string wbProductId, string? wbRootId, string? sourceCategory, string? sourceSubcategory, string rankHash, string rankJson, DateTime observedAtUtc, string batchId)
    {
        Id = Guid.NewGuid();
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        RankHash = rankHash;
        RankJson = rankJson;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string RankHash { get; private set; } = string.Empty;
    public string RankJson { get; private set; } = "{}";
    public DateTime ObservedAtUtc { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string? wbRootId, string? sourceCategory, string? sourceSubcategory, string rankHash, string rankJson, DateTime observedAtUtc, string batchId)
    {
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        RankHash = rankHash;
        RankJson = rankJson;
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
