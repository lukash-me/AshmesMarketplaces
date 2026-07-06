using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProductPresenceEvent
{
    private const int MaxReasonLength = 160;

    private ParserProductPresenceEvent() { }

    public ParserProductPresenceEvent(
        string wbProductId,
        string parserCycleId,
        string? sourceCategory,
        string? sourceSubcategory,
        string oldStatus,
        string newStatus,
        string reason,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));
        if (string.IsNullOrWhiteSpace(parserCycleId))
            throw new ArgumentException("Parser cycle id is required.", nameof(parserCycleId));
        if (string.IsNullOrWhiteSpace(oldStatus))
            throw new ArgumentException("Old status is required.", nameof(oldStatus));
        if (string.IsNullOrWhiteSpace(newStatus))
            throw new ArgumentException("New status is required.", nameof(newStatus));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));
        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        WbProductId = wbProductId.Trim();
        ParserCycleId = parserCycleId.Trim();
        SourceCategory = NormalizeOptional(sourceCategory);
        SourceSubcategory = NormalizeOptional(sourceSubcategory);
        OldStatus = oldStatus.Trim();
        NewStatus = newStatus.Trim();
        Reason = NormalizeReason(reason);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string ParserCycleId { get; private set; } = string.Empty;
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string OldStatus { get; private set; } = string.Empty;
    public string NewStatus { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeReason(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= MaxReasonLength ? normalized : normalized[..MaxReasonLength];
    }
}
