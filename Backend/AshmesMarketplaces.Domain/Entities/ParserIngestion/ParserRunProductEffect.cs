namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserRunProductEffect
{
    private ParserRunProductEffect() { }

    public ParserRunProductEffect(
        Guid parserProxyRunId,
        Guid parserBatchSubmissionId,
        string wbProductId,
        Guid serviceProductId,
        string effectType,
        DateTime createdAtUtc)
    {
        if (parserProxyRunId == Guid.Empty)
            throw new ArgumentException("Parser proxy run id is required.", nameof(parserProxyRunId));
        if (parserBatchSubmissionId == Guid.Empty)
            throw new ArgumentException("Parser batch submission id is required.", nameof(parserBatchSubmissionId));
        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));
        if (serviceProductId == Guid.Empty)
            throw new ArgumentException("Service product id is required.", nameof(serviceProductId));

        Id = Guid.NewGuid();
        ParserProxyRunId = parserProxyRunId;
        ParserBatchSubmissionId = parserBatchSubmissionId;
        WbProductId = wbProductId.Trim();
        ServiceProductId = serviceProductId;
        EffectType = NormalizeEffectType(effectType);
        RollbackStatus = ParserRunEffectRollbackStatuses.Pending;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }

    public Guid Id { get; private set; }
    public Guid ParserProxyRunId { get; private set; }
    public Guid ParserBatchSubmissionId { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public Guid ServiceProductId { get; private set; }
    public string EffectType { get; private set; } = string.Empty;
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

    private static string NormalizeEffectType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Effect type is required.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is ParserRunProductEffectTypes.Created or ParserRunProductEffectTypes.Updated
            ? normalized
            : throw new ArgumentException("Unsupported parser run product effect type.", nameof(value));
    }
}

public static class ParserRunProductEffectTypes
{
    public const string Created = "created";
    public const string Updated = "updated";
}

public static class ParserRunEffectRollbackStatuses
{
    public const string Pending = "pending";
    public const string RolledBack = "rolled_back";
}
