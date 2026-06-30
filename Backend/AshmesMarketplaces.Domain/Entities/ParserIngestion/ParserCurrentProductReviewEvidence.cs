namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserCurrentProductReviewEvidence
{
    private ParserCurrentProductReviewEvidence() { }

    public ParserCurrentProductReviewEvidence(
        string wbProductId,
        string? wbRootId,
        string reviewIdOnMp,
        string reviewHash,
        string reviewJson,
        int? rating,
        DateTime? createdAtOnMp,
        DateTime observedAtUtc,
        string batchId)
    {
        Id = Guid.NewGuid();
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        ReviewIdOnMp = reviewIdOnMp;
        ReviewHash = reviewHash;
        ReviewJson = reviewJson;
        Rating = rating;
        CreatedAtOnMp = createdAtOnMp;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string ReviewIdOnMp { get; private set; } = string.Empty;
    public string ReviewHash { get; private set; } = string.Empty;
    public string ReviewJson { get; private set; } = "{}";
    public int? Rating { get; private set; }
    public DateTime? CreatedAtOnMp { get; private set; }
    public DateTime ObservedAtUtc { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string? wbRootId, string reviewHash, string reviewJson, int? rating, DateTime? createdAtOnMp, DateTime observedAtUtc, string batchId)
    {
        WbRootId = wbRootId;
        ReviewHash = reviewHash;
        ReviewJson = reviewJson;
        Rating = rating;
        CreatedAtOnMp = createdAtOnMp;
        ObservedAtUtc = observedAtUtc;
        BatchId = batchId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
