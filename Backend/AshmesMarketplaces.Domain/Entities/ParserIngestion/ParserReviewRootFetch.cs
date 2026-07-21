using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserReviewRootFetch : IDisposable
{
    private ParserReviewRootFetch() { }

    public ParserReviewRootFetch(
        Guid idParserRun,
        Guid idParserFile,
        long sourceLineNumber,
        string rowHash,
        DateTime timestampUtc,
        string parserRunId,
        string sourceWbRootId,
        JsonDocument? selectedWbProductIds,
        string endpoint,
        int attempts,
        int retries,
        decimal backoffSecondsTotal,
        int? httpStatus,
        string status,
        int elapsedMs,
        int? payloadFeedbackCount,
        int payloadFeedbackRowsSeen,
        int selectedReviewRowsSeen,
        int reviewsWritten,
        int repliesWritten,
        string? rawPayloadPath,
        string? errorSummary,
        bool isPartialSnapshot,
        bool isCappedRootPayload,
        bool isFullHistoryUnknown)
    {
        ParserStagingGuard.EnsureSource(idParserRun, idParserFile, sourceLineNumber, rowHash);

        if (string.IsNullOrWhiteSpace(parserRunId))
            throw new ArgumentException("Parser run id is required.", nameof(parserRunId));

        if (string.IsNullOrWhiteSpace(sourceWbRootId))
            throw new ArgumentException("Source WB root id is required.", nameof(sourceWbRootId));

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint is required.", nameof(endpoint));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Fetch status is required.", nameof(status));

        if (attempts < 0 || retries < 0 || elapsedMs < 0)
            throw new ArgumentOutOfRangeException(nameof(attempts), "Fetch counters must be non-negative.");

        DateTimeUtc.EnsureUtc(timestampUtc, nameof(timestampUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        SourceLineNumber = sourceLineNumber;
        RowHash = rowHash;
        TimestampUtc = timestampUtc;
        ParserRunId = parserRunId;
        SourceWbRootId = sourceWbRootId;
        SelectedWbProductIds = selectedWbProductIds;
        Endpoint = endpoint;
        Attempts = attempts;
        Retries = retries;
        BackoffSecondsTotal = backoffSecondsTotal;
        HttpStatus = httpStatus;
        Status = status;
        ElapsedMs = elapsedMs;
        PayloadFeedbackCount = payloadFeedbackCount;
        PayloadFeedbackRowsSeen = payloadFeedbackRowsSeen;
        SelectedReviewRowsSeen = selectedReviewRowsSeen;
        ReviewsWritten = reviewsWritten;
        RepliesWritten = repliesWritten;
        RawPayloadPath = rawPayloadPath;
        ErrorSummary = errorSummary;
        IsPartialSnapshot = isPartialSnapshot;
        IsCappedRootPayload = isCappedRootPayload;
        IsFullHistoryUnknown = isFullHistoryUnknown;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public Guid IdParserFile { get; private set; }
    public long SourceLineNumber { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public string SourceWbRootId { get; private set; } = string.Empty;
    public JsonDocument? SelectedWbProductIds { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public int Retries { get; private set; }
    public decimal BackoffSecondsTotal { get; private set; }
    public int? HttpStatus { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int ElapsedMs { get; private set; }
    public int? PayloadFeedbackCount { get; private set; }
    public int PayloadFeedbackRowsSeen { get; private set; }
    public int SelectedReviewRowsSeen { get; private set; }
    public int ReviewsWritten { get; private set; }
    public int RepliesWritten { get; private set; }
    public string? RawPayloadPath { get; private set; }
    public string? ErrorSummary { get; private set; }
    public bool IsPartialSnapshot { get; private set; }
    public bool IsCappedRootPayload { get; private set; }
    public bool IsFullHistoryUnknown { get; private set; }

    public void Dispose()
    {
        SelectedWbProductIds?.Dispose();
    }
}
