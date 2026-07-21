using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserLogisticsSnapshotRow : IDisposable
{
    private ParserLogisticsSnapshotRow() { }

    public ParserLogisticsSnapshotRow(
        Guid idParserRun,
        Guid idParserFile,
        long sourceLineNumber,
        string rowHash,
        int schemaVersion,
        string parserRunId,
        string marketplace,
        DateTime observedAtUtc,
        string sourceRequestFamily,
        string sourceEndpoint,
        string requestFingerprint,
        string sourceRegionDest,
        string? deliveryProfileKey,
        string? deliveryDestinationName,
        string? deliveryProfileVersion,
        string? deliveryDestinationCity,
        string? deliveryDestinationLabel,
        string? deliveryDestinationAddress,
        decimal? deliveryDestinationLatitude,
        decimal? deliveryDestinationLongitude,
        string? sourceCategory,
        string? sourceSubcategory,
        string? sourceQuery,
        string wbProductId,
        string? wbRootId,
        string? sellerId,
        string? sellerName,
        int? totalQuantityObserved,
        bool? quantityIsCapped,
        int? quantityCapObserved,
        string quantitySemantics,
        string? productWhRaw,
        int? productTime1Raw,
        int? productTime2Raw,
        long? productDtypeRaw,
        int? productDistRaw,
        string? visibleDeliveryStatus,
        string? visibleDeliveryLabel,
        DateTime? visibleDeliveryDate,
        string? visibleDeliverySource,
        DateTime? visibleDeliveryObservedAtUtc,
        JsonDocument? visibleDeliveryRawPayload,
        JsonDocument? rawObservedFields)
    {
        ParserStagingGuard.EnsureSource(idParserRun, idParserFile, sourceLineNumber, rowHash);

        if (string.IsNullOrWhiteSpace(parserRunId))
            throw new ArgumentException("Parser run id is required.", nameof(parserRunId));

        if (string.IsNullOrWhiteSpace(marketplace))
            throw new ArgumentException("Marketplace is required.", nameof(marketplace));

        if (string.IsNullOrWhiteSpace(sourceRequestFamily))
            throw new ArgumentException("Source request family is required.", nameof(sourceRequestFamily));

        if (string.IsNullOrWhiteSpace(sourceEndpoint))
            throw new ArgumentException("Source endpoint is required.", nameof(sourceEndpoint));

        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new ArgumentException("Request fingerprint is required.", nameof(requestFingerprint));

        if (string.IsNullOrWhiteSpace(sourceRegionDest))
            throw new ArgumentException("Source region dest is required.", nameof(sourceRegionDest));

        if (string.IsNullOrWhiteSpace(wbProductId))
            throw new ArgumentException("WB product id is required.", nameof(wbProductId));

        if (string.IsNullOrWhiteSpace(quantitySemantics))
            throw new ArgumentException("Quantity semantics is required.", nameof(quantitySemantics));

        DateTimeUtc.EnsureUtc(observedAtUtc, nameof(observedAtUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        SourceLineNumber = sourceLineNumber;
        RowHash = rowHash;
        SchemaVersion = schemaVersion;
        ParserRunId = parserRunId;
        Marketplace = marketplace;
        ObservedAtUtc = observedAtUtc;
        SourceRequestFamily = sourceRequestFamily;
        SourceEndpoint = sourceEndpoint;
        RequestFingerprint = requestFingerprint;
        SourceRegionDest = sourceRegionDest;
        DeliveryProfileKey = deliveryProfileKey;
        DeliveryDestinationName = deliveryDestinationName;
        DeliveryProfileVersion = deliveryProfileVersion;
        DeliveryDestinationCity = deliveryDestinationCity;
        DeliveryDestinationLabel = deliveryDestinationLabel;
        DeliveryDestinationAddress = deliveryDestinationAddress;
        DeliveryDestinationLatitude = deliveryDestinationLatitude;
        DeliveryDestinationLongitude = deliveryDestinationLongitude;
        SourceCategory = sourceCategory;
        SourceSubcategory = sourceSubcategory;
        SourceQuery = sourceQuery;
        WbProductId = wbProductId;
        WbRootId = wbRootId;
        SellerId = sellerId;
        SellerName = sellerName;
        TotalQuantityObserved = totalQuantityObserved;
        QuantityIsCapped = quantityIsCapped;
        QuantityCapObserved = quantityCapObserved;
        QuantitySemantics = quantitySemantics;
        ProductWhRaw = productWhRaw;
        ProductTime1Raw = productTime1Raw;
        ProductTime2Raw = productTime2Raw;
        ProductDtypeRaw = productDtypeRaw;
        ProductDistRaw = productDistRaw;
        VisibleDeliveryStatus = visibleDeliveryStatus;
        VisibleDeliveryLabel = visibleDeliveryLabel;
        VisibleDeliveryDate = visibleDeliveryDate;
        VisibleDeliverySource = visibleDeliverySource;
        VisibleDeliveryObservedAtUtc = visibleDeliveryObservedAtUtc;
        VisibleDeliveryRawPayload = visibleDeliveryRawPayload;
        RawObservedFields = rawObservedFields;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public Guid IdParserFile { get; private set; }
    public long SourceLineNumber { get; private set; }
    public string RowHash { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string ParserRunId { get; private set; } = string.Empty;
    public string Marketplace { get; private set; } = string.Empty;
    public DateTime ObservedAtUtc { get; private set; }
    public string SourceRequestFamily { get; private set; } = string.Empty;
    public string SourceEndpoint { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string SourceRegionDest { get; private set; } = string.Empty;
    public string? DeliveryProfileKey { get; private set; }
    public string? DeliveryDestinationName { get; private set; }
    public string? DeliveryProfileVersion { get; private set; }
    public string? DeliveryDestinationCity { get; private set; }
    public string? DeliveryDestinationLabel { get; private set; }
    public string? DeliveryDestinationAddress { get; private set; }
    public decimal? DeliveryDestinationLatitude { get; private set; }
    public decimal? DeliveryDestinationLongitude { get; private set; }
    public string? SourceCategory { get; private set; }
    public string? SourceSubcategory { get; private set; }
    public string? SourceQuery { get; private set; }
    public string WbProductId { get; private set; } = string.Empty;
    public string? WbRootId { get; private set; }
    public string? SellerId { get; private set; }
    public string? SellerName { get; private set; }
    public int? TotalQuantityObserved { get; private set; }
    public bool? QuantityIsCapped { get; private set; }
    public int? QuantityCapObserved { get; private set; }
    public string QuantitySemantics { get; private set; } = string.Empty;
    public string? ProductWhRaw { get; private set; }
    public int? ProductTime1Raw { get; private set; }
    public int? ProductTime2Raw { get; private set; }
    public long? ProductDtypeRaw { get; private set; }
    public int? ProductDistRaw { get; private set; }
    public string? VisibleDeliveryStatus { get; private set; }
    public string? VisibleDeliveryLabel { get; private set; }
    public DateTime? VisibleDeliveryDate { get; private set; }
    public string? VisibleDeliverySource { get; private set; }
    public DateTime? VisibleDeliveryObservedAtUtc { get; private set; }
    public JsonDocument? VisibleDeliveryRawPayload { get; private set; }
    public JsonDocument? RawObservedFields { get; private set; }

    public void Dispose()
    {
        VisibleDeliveryRawPayload?.Dispose();
        RawObservedFields?.Dispose();
    }
}
