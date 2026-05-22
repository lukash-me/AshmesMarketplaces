using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserReviewReplyRow : IDisposable
{
    private ParserReviewReplyRow() { }

    public ParserReviewReplyRow(
        Guid idParserRun,
        Guid idParserFile,
        Guid? idReviewRootFetch,
        long sourceLineNumber,
        string rowHash,
        int schemaVersion,
        string parserRunId,
        DateTime parsedAtUtc,
        string marketplace,
        string? inputProductsParserRunId,
        string? inputProductsJsonl,
        string sourceWbRootId,
        string wbProductId,
        string reviewAttributionMode,
        string reviewIdOnMp,
        string? replyIdOnMp,
        string? replyFallbackHash,
        string? text,
        DateTime? createdAtOnMp,
        DateTime? updatedAtOnMp,
        string? replyAuthor,
        string? replyState,
        string? sourceCategory,
        string? sourceSubcategory,
        string? sourceQuery,
        string? sourceRegionDest,
        JsonDocument? rawObservedFields,
        bool isPartialSnapshot,
        bool isCappedRootPayload,
        bool isFullHistoryUnknown)
    {
        ParserStagingGuard.EnsureSource(idParserRun, idParserFile, sourceLineNumber, rowHash);

        if (string.IsNullOrWhiteSpace(parserRunId)
            || string.IsNullOrWhiteSpace(marketplace)
            || string.IsNullOrWhiteSpace(sourceWbRootId)
            || string.IsNullOrWhiteSpace(wbProductId)
            || string.IsNullOrWhiteSpace(reviewAttributionMode)
            || string.IsNullOrWhiteSpace(reviewIdOnMp))
        {
            throw new ArgumentException("Reply identity and attribution fields are required.");
        }

        if (string.IsNullOrWhiteSpace(replyIdOnMp) && string.IsNullOrWhiteSpace(replyFallbackHash))
            throw new ArgumentException("Reply id or fallback hash is required.");

        DateTimeUtc.EnsureUtc(parsedAtUtc, nameof(parsedAtUtc));
        DateTimeUtc.EnsureUtc(createdAtOnMp, nameof(createdAtOnMp));
        DateTimeUtc.EnsureUtc(updatedAtOnMp, nameof(updatedAtOnMp));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        IdReviewRootFetch = idReviewRootFetch;
        SourceLineNumber = sourceLineNumber;
        RowHash = rowHash;
        SchemaVersion = schemaVersion;
        ParserRunId = parserRunId;
        ParsedAtUtc = parsedAtUtc;
        Marketplace = marketplace;
        InputProductsParserRunId = inputProductsParserRunId;
        InputProductsJsonl = inputProductsJsonl;
        SourceWbRootId = sourceWbRootId;
        WbProductId = wbProductId;
        ReviewAttributionMode = reviewAttributionMode;
        ReviewIdOnMp = reviewIdOnMp;
        ReplyIdOnMp = replyIdOnMp;
        ReplyFallbackHash = replyFallbackHash;
        Text = text;
        CreatedAtOnMp = createdAtOnMp;
        UpdatedAtOnMp = updatedAtOnMp;
        ReplyAuthor = replyAuthor;
        ReplyState = replyState;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        SourceQuery = sourceQuery;
        SourceRegionDest = sourceRegionDest;
        RawObservedFields = rawObservedFields;
        IsPartialSnapshot = isPartialSnapshot;
        IsCappedRootPayload = isCappedRootPayload;
        IsFullHistoryUnknown = isFullHistoryUnknown;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public Guid IdParserFile { get; private set; }
    public Guid? IdReviewRootFetch { get; private set; }
    public long SourceLineNumber { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public DateTime ParsedAtUtc { get; private set; }
    public string Marketplace { get; private set; } = string.Empty;
    public string? InputProductsParserRunId { get; private set; }
    public string? InputProductsJsonl { get; private set; }
    public string SourceWbRootId { get; private set; } = string.Empty;
    public string WbProductId { get; private set; } = string.Empty;
    public string ReviewAttributionMode { get; private set; } = string.Empty;
    public string ReviewIdOnMp { get; private set; } = string.Empty;
    public string? ReplyIdOnMp { get; private set; }
    public string? ReplyFallbackHash { get; private set; }
    public string? Text { get; private set; }
    public DateTime? CreatedAtOnMp { get; private set; }
    public DateTime? UpdatedAtOnMp { get; private set; }
    public string? ReplyAuthor { get; private set; }
    public string? ReplyState { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string? SourceQuery { get; private set; }
    public string? SourceRegionDest { get; private set; }
    public JsonDocument? RawObservedFields { get; private set; }
    public bool IsPartialSnapshot { get; private set; }
    public bool IsCappedRootPayload { get; private set; }
    public bool IsFullHistoryUnknown { get; private set; }

    public void Dispose()
    {
        RawObservedFields?.Dispose();
    }
}
