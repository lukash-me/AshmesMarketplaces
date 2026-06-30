using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserBatchSubmission
{
    private ParserBatchSubmission() { }

    public ParserBatchSubmission(
        string parserInstanceId,
        string externalBatchId,
        string sourceCategory,
        string sourceSubcategory,
        string? proxyKey,
        string batchKind,
        string contentHash,
        DateTime acceptedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(parserInstanceId))
            throw new ArgumentException("Parser instance id is required.", nameof(parserInstanceId));
        if (string.IsNullOrWhiteSpace(externalBatchId))
            throw new ArgumentException("External batch id is required.", nameof(externalBatchId));
        if (string.IsNullOrWhiteSpace(sourceCategory))
            throw new ArgumentException("Source category is required.", nameof(sourceCategory));
        if (string.IsNullOrWhiteSpace(sourceSubcategory))
            throw new ArgumentException("Source subcategory is required.", nameof(sourceSubcategory));
        if (string.IsNullOrWhiteSpace(batchKind))
            throw new ArgumentException("Batch kind is required.", nameof(batchKind));
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new ArgumentException("Content hash is required.", nameof(contentHash));

        DateTimeUtc.EnsureUtc(acceptedAtUtc, nameof(acceptedAtUtc));

        Id = Guid.NewGuid();
        ParserInstanceId = parserInstanceId.Trim();
        ExternalBatchId = externalBatchId.Trim();
        SourceCategory = sourceCategory.Trim();
        SourceSubcategory = sourceSubcategory.Trim();
        ProxyKey = string.IsNullOrWhiteSpace(proxyKey) ? null : proxyKey.Trim();
        BatchKind = batchKind.Trim();
        ContentHash = contentHash.Trim();
        Status = ParserBatchStatuses.Accepted;
        AttemptsCount = 0;
        AcceptedAtUtc = acceptedAtUtc;
        CreatedAtUtc = acceptedAtUtc;
        UpdatedAtUtc = acceptedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ParserInstanceId { get; private set; } = string.Empty;
    public string ExternalBatchId { get; private set; } = string.Empty;
    public string SourceCategory { get; private set; } = string.Empty;
    public string SourceSubcategory { get; private set; } = string.Empty;
    public string? ProxyKey { get; private set; }
    public string BatchKind { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int AttemptsCount { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }
    public DateTime? ProcessingStartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkQueued(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        Status = ParserBatchStatuses.Queued;
        UpdatedAtUtc = nowUtc;
    }

    public void Retry(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        Status = ParserBatchStatuses.Queued;
        ProcessingStartedAtUtc = null;
        CompletedAtUtc = null;
        Error = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkProcessing(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        Status = ParserBatchStatuses.Processing;
        ProcessingStartedAtUtc = nowUtc;
        CompletedAtUtc = null;
        Error = null;
        AttemptsCount++;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCompleted(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        Status = ParserBatchStatuses.Completed;
        CompletedAtUtc = nowUtc;
        Error = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailedRetryable(string error, DateTime nowUtc)
    {
        MarkFailed(ParserBatchStatuses.FailedRetryable, error, nowUtc);
    }

    public void MarkFailedFinal(string error, DateTime nowUtc)
    {
        MarkFailed(ParserBatchStatuses.FailedFinal, error, nowUtc);
    }

    public void MarkAcknowledged(DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        AcknowledgedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    private void MarkFailed(string status, string error, DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        Status = status;
        Error = string.IsNullOrWhiteSpace(error) ? "Batch processing failed." : error.Trim();
        CompletedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    private static void EnsureUtc(DateTime value)
    {
        DateTimeUtc.EnsureUtc(value, nameof(value));
    }
}
