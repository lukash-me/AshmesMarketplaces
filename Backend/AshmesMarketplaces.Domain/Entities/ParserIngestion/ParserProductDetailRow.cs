using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserProductDetailRow : IDisposable
{
    private ParserProductDetailRow() { }

    public ParserProductDetailRow(
        Guid idParserRun,
        Guid idParserFile,
        long sourceLineNumber,
        string rowHash,
        int schemaVersion,
        string parserRunId,
        DateTime parsedAtUtc,
        string marketplace,
        string? inputProductsParserRunId,
        string? inputProductsJsonl,
        string sourceRequestFamily,
        string sourceEndpoint,
        string requestFingerprint,
        string wbProductId,
        string? wbRootId,
        string? sourceCategory,
        string? sourceSubcategory,
        string? sourceQuery,
        string? sourceRegionDest,
        string? description,
        JsonDocument? characteristics,
        JsonDocument? groupedOptions,
        int? mediaCount,
        string status,
        JsonDocument? rawDetailFields)
    {
        ParserStagingGuard.EnsureSource(idParserRun, idParserFile, sourceLineNumber, rowHash);

        if (string.IsNullOrWhiteSpace(parserRunId)
            || string.IsNullOrWhiteSpace(marketplace)
            || string.IsNullOrWhiteSpace(sourceRequestFamily)
            || string.IsNullOrWhiteSpace(sourceEndpoint)
            || string.IsNullOrWhiteSpace(requestFingerprint)
            || string.IsNullOrWhiteSpace(wbProductId)
            || string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Product detail identity and source fields are required.");
        }

        DateTimeUtc.EnsureUtc(parsedAtUtc, nameof(parsedAtUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        SourceLineNumber = sourceLineNumber;
        RowHash = rowHash;
        SchemaVersion = schemaVersion;
        ParserRunId = parserRunId;
        ParsedAtUtc = parsedAtUtc;
        Marketplace = marketplace;
        InputProductsParserRunId = inputProductsParserRunId;
        InputProductsJsonl = inputProductsJsonl;
        SourceRequestFamily = sourceRequestFamily;
        SourceEndpoint = sourceEndpoint;
        RequestFingerprint = requestFingerprint;
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        SourceQuery = sourceQuery;
        SourceRegionDest = sourceRegionDest;
        Description = description;
        Characteristics = characteristics;
        GroupedOptions = groupedOptions;
        MediaCount = mediaCount;
        Status = status;
        RawDetailFields = rawDetailFields;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public Guid IdParserFile { get; private set; }
    public long SourceLineNumber { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public DateTime ParsedAtUtc { get; private set; }
    public string Marketplace { get; private set; } = string.Empty;
    public string? InputProductsParserRunId { get; private set; }
    public string? InputProductsJsonl { get; private set; }
    public string SourceRequestFamily { get; private set; } = string.Empty;
    public string SourceEndpoint { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string? SourceQuery { get; private set; }
    public string? SourceRegionDest { get; private set; }
    public string? Description { get; private set; }
    public JsonDocument? Characteristics { get; private set; }
    public JsonDocument? GroupedOptions { get; private set; }
    public int? MediaCount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public JsonDocument? RawDetailFields { get; private set; }

    public void Dispose()
    {
        Characteristics?.Dispose();
        GroupedOptions?.Dispose();
        RawDetailFields?.Dispose();
    }
}
