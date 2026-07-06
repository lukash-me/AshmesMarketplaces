namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserRunCurrentEntityEffect
{
    private ParserRunCurrentEntityEffect() { }

    public ParserRunCurrentEntityEffect(
        Guid parserProxyRunId,
        Guid parserBatchSubmissionId,
        string wbProductId,
        string entityKind,
        string entityKey,
        string effectType,
        string? oldValueJson,
        string? newValueJson,
        DateTime createdAtUtc)
    {
        if (parserProxyRunId == Guid.Empty)
            throw new ArgumentException("Parser proxy run id is required.", nameof(parserProxyRunId));
        if (parserBatchSubmissionId == Guid.Empty)
            throw new ArgumentException("Parser batch submission id is required.", nameof(parserBatchSubmissionId));
        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));
        if (string.IsNullOrWhiteSpace(entityKey))
            throw new ArgumentException("Entity key is required.", nameof(entityKey));

        Id = Guid.NewGuid();
        ParserProxyRunId = parserProxyRunId;
        ParserBatchSubmissionId = parserBatchSubmissionId;
        WbProductId = wbProductId.Trim();
        EntityKind = NormalizeEntityKind(entityKind);
        EntityKey = entityKey.Trim();
        EffectType = NormalizeEffectType(effectType);
        OldValueJson = string.IsNullOrWhiteSpace(oldValueJson) ? null : oldValueJson;
        NewValueJson = string.IsNullOrWhiteSpace(newValueJson) ? null : newValueJson;
        RollbackStatus = ParserRunEffectRollbackStatuses.Pending;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }

    public Guid Id { get; private set; }
    public Guid ParserProxyRunId { get; private set; }
    public Guid ParserBatchSubmissionId { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string EntityKind { get; private set; } = string.Empty;
    public string EntityKey { get; private set; } = string.Empty;
    public string EffectType { get; private set; } = string.Empty;
    public string? OldValueJson { get; private set; }
    public string? NewValueJson { get; private set; }
    public string RollbackStatus { get; private set; } = ParserRunEffectRollbackStatuses.Pending;
    public Guid? RollbackId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RolledBackAtUtc { get; private set; }

    public void MarkRolledBack(Guid rollbackId, DateTime nowUtc)
    {
        if (rollbackId == Guid.Empty)
            throw new ArgumentException("Rollback id is required.", nameof(rollbackId));

        RollbackId = rollbackId;
        RollbackStatus = ParserRunEffectRollbackStatuses.RolledBack;
        RolledBackAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }

    private static string NormalizeEntityKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Entity kind is required.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is ParserRunCurrentEntityKinds.Product
            or ParserRunCurrentEntityKinds.Details
            or ParserRunCurrentEntityKinds.Logistics
            or ParserRunCurrentEntityKinds.Rank
            or ParserRunCurrentEntityKinds.ReviewsSummary
            or ParserRunCurrentEntityKinds.ReviewEvidence
            ? normalized
            : throw new ArgumentException("Unsupported parser run current entity kind.", nameof(value));
    }

    private static string NormalizeEffectType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Effect type is required.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is ParserRunCurrentEntityEffectTypes.Created or ParserRunCurrentEntityEffectTypes.Updated
            ? normalized
            : throw new ArgumentException("Unsupported parser run current entity effect type.", nameof(value));
    }
}

public static class ParserRunCurrentEntityKinds
{
    public const string Product = "product";
    public const string Details = "details";
    public const string Logistics = "logistics";
    public const string Rank = "rank";
    public const string ReviewsSummary = "reviews_summary";
    public const string ReviewEvidence = "review_evidence";
}

public static class ParserRunCurrentEntityEffectTypes
{
    public const string Created = "created";
    public const string Updated = "updated";
}
