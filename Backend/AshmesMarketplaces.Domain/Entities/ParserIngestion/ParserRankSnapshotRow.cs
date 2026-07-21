using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserRankSnapshotRow : IDisposable
{
    private ParserRankSnapshotRow() { }

    public ParserRankSnapshotRow(
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
        int positionOnPage,
        int absolutePosition,
        string wbProductId,
        string? wbRootId,
        int? responseTotal,
        string fetchStatus)
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

        if (page < 1 || positionOnPage < 1 || absolutePosition < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Rank positions must be positive observed values.");

        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));

        if (responseTotal is < 0)
            throw new ArgumentOutOfRangeException(nameof(responseTotal), "Response total must be non-negative.");

        if (string.IsNullOrWhiteSpace(fetchStatus))
            throw new ArgumentException("Fetch status is required.", nameof(fetchStatus));

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
        PositionOnPage = positionOnPage;
        AbsolutePosition = absolutePosition;
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        ResponseTotal = responseTotal;
        FetchStatus = fetchStatus;
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
    public int PositionOnPage { get; private set; }
    public int AbsolutePosition { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public int? ResponseTotal { get; private set; }
    public string FetchStatus { get; private set; } = string.Empty;

    public void Dispose()
    {
        Filters?.Dispose();
    }
}
