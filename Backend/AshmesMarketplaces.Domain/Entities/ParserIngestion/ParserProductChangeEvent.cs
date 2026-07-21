using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProductChangeEvent
{
    private ParserProductChangeEvent() { }

    public ParserProductChangeEvent(
        string wbProductId,
        string? wbRootId,
        string batchId,
        string? sourceCategory,
        string? sourceSubcategory,
        string fieldGroup,
        string changeType,
        string? oldHash,
        string newHash,
        string? oldValueJson,
        string? newValueJson,
        DateTime observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));
        if (string.IsNullOrWhiteSpace(batchId))
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        if (string.IsNullOrWhiteSpace(fieldGroup))
            throw new ArgumentException("Field group is required.", nameof(fieldGroup));
        if (string.IsNullOrWhiteSpace(changeType))
            throw new ArgumentException("Change type is required.", nameof(changeType));
        if (string.IsNullOrWhiteSpace(newHash))
            throw new ArgumentException("New hash is required.", nameof(newHash));

        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));

        Id = Guid.NewGuid();
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        BatchId = batchId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        FieldGroup = fieldGroup;
        ChangeType = changeType;
        OldHash = oldHash;
        NewHash = newHash;
        OldValueJson = oldValueJson;
        NewValueJson = newValueJson;
        ObservedAtUtc = observedAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string BatchId { get; private set; } = string.Empty;
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string FieldGroup { get; private set; } = string.Empty;
    public string ChangeType { get; private set; } = string.Empty;
    public string? OldHash { get; private set; }
    public string NewHash { get; private set; } = string.Empty;
    public string? OldValueJson { get; private set; }
    public string? NewValueJson { get; private set; }
    public DateTime ObservedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
