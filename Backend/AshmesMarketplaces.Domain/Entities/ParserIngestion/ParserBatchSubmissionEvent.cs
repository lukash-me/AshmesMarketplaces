using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserBatchSubmissionEvent
{
    private ParserBatchSubmissionEvent() { }

    public ParserBatchSubmissionEvent(Guid batchSubmissionId, string eventType, string status, string? message, DateTime createdAtUtc)
    {
        if (batchSubmissionId == Guid.Empty)
            throw new ArgumentException("Batch submission id is required.", nameof(batchSubmissionId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        DateTimeUtc.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        BatchSubmissionId = batchSubmissionId;
        EventType = eventType.Trim();
        Status = status.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BatchSubmissionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
