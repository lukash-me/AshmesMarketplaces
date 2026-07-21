using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserReviewRow : IDisposable
{
    private ParserReviewRow() { }

    public ParserReviewRow(
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
        int? rating,
        string? text,
        string? pros,
        string? cons,
        DateTime? createdAtOnMp,
        string? reviewerName,
        string? reviewerCountry,
        bool? reviewerHasPhoto,
        int? helpfulPlus,
        int? helpfulMinus,
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
            throw new ArgumentException("Review identity and attribution fields are required.");
        }

        DateTimeUtc.EnsureUtc(parsedAtUtc, nameof(parsedAtUtc));
        DateTimeUtc.EnsureUtc(createdAtOnMp, nameof(createdAtOnMp));

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
        Rating = rating;
        Text = text;
        Pros = pros;
        Cons = cons;
        CreatedAtOnMp = createdAtOnMp;
        ReviewerName = reviewerName;
        ReviewerCountry = reviewerCountry;
        ReviewerHasPhoto = reviewerHasPhoto;
        HelpfulPlus = helpfulPlus;
        HelpfulMinus = helpfulMinus;
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
    public int? Rating { get; private set; }
    public string? Text { get; private set; }
    public string? Pros { get; private set; }
    public string? Cons { get; private set; }
    public DateTime? CreatedAtOnMp { get; private set; }
    public string? ReviewerName { get; private set; }
    public string? ReviewerCountry { get; private set; }
    public bool? ReviewerHasPhoto { get; private set; }
    public int? HelpfulPlus { get; private set; }
    public int? HelpfulMinus { get; private set; }
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
