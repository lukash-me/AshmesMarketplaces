using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserRankPageFetch : IDisposable
{
    private ParserRankPageFetch() { }

    public ParserRankPageFetch(
        Guid idParserRun,
        Guid idParserFile,
        long sourceLineNumber,
        string rowHash,
        int schemaVersion,
        string parserRunId,
        DateTime observedAtUtc,
        string marketplace,
        string rankContextId,
        string rankContextType,
        string? sourceCategory,
        string? sourceSubcategory,
        string query,
        string? sourceRegionDest,
        string? sort,
        JsonDocument? filters,
        string requestFingerprint,
        int page,
        string status,
        int productCount,
        int? responseTotal,
        int retryCount,
        int? httpStatus,
        string? wbCode,
        string? message)
    {
        ParserStagingGuard.EnsureSource(idParserRun, idParserFile, sourceLineNumber, rowHash);

        if (string.IsNullOrWhiteSpace(parserRunId))
            throw new ArgumentException("Parser run id is required.", nameof(parserRunId));

        if (string.IsNullOrWhiteSpace(marketplace))
            throw new ArgumentException("Marketplace is required.", nameof(marketplace));

        if (string.IsNullOrWhiteSpace(rankContextId))
            throw new ArgumentException("Rank context id is required.", nameof(rankContextId));

        if (string.IsNullOrWhiteSpace(rankContextType))
            throw new ArgumentException("Rank context type is required.", nameof(rankContextType));

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Rank query is required.", nameof(query));

        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new ArgumentException("Request fingerprint is required.", nameof(requestFingerprint));

        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Rank page must be positive.");

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Page fetch status is required.", nameof(status));

        if (productCount < 0 || retryCount < 0 || responseTotal is < 0)
            throw new ArgumentOutOfRangeException(nameof(productCount), "Page fetch counters must be non-negative.");

        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        SourceLineNumber = sourceLineNumber;
        RowHash = rowHash;
        SchemaVersion = schemaVersion;
        ParserRunId = parserRunId;
        ObservedAtUtc = observedAtUtc;
        Marketplace = marketplace;
        RankContextId = rankContextId;
        RankContextType = rankContextType;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        Query = query;
        SourceRegionDest = sourceRegionDest;
        Sort = sort;
        Filters = filters;
        RequestFingerprint = requestFingerprint;
        Page = page;
        Status = status;
        ProductCount = productCount;
        ResponseTotal = responseTotal;
        RetryCount = retryCount;
        HttpStatus = httpStatus;
        WbCode = wbCode;
        Message = message;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public Guid IdParserFile { get; private set; }
    public long SourceLineNumber { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public DateTime ObservedAtUtc { get; private set; }
    public string Marketplace { get; private set; } = string.Empty;
    public string RankContextId { get; private set; } = string.Empty;
    public string RankContextType { get; private set; } = string.Empty;
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public string? SourceRegionDest { get; private set; }
    public string? Sort { get; private set; }
    public JsonDocument? Filters { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public int Page { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int ProductCount { get; private set; }
    public int? ResponseTotal { get; private set; }
    public int RetryCount { get; private set; }
    public int? HttpStatus { get; private set; }
    public string? WbCode { get; private set; }
    public string? Message { get; private set; }

    public void Dispose()
    {
        Filters?.Dispose();
    }
}
