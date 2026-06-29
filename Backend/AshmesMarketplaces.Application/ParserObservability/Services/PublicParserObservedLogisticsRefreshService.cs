using System.Diagnostics;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class PublicParserObservedLogisticsRefreshService : IPublicParserObservedLogisticsRefreshService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public PublicParserObservedLogisticsRefreshService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicParserObservedLogisticsRefreshResult>> RefreshAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var eventResponse = await BuildMarketEventsAsync(cancellationToken);
        var stockDecreaseResponse = BuildStockDecreaseResponse(eventResponse);
        stopwatch.Stop();

        var nowUtc = DateTime.UtcNow;
        var snapshot = await _dbContext.PublicParserObservedLogisticsSnapshots
            .FirstOrDefaultAsync(x => x.SnapshotKey == PublicParserObservedLogisticsSnapshot.SnapshotKeyValue, cancellationToken)
            ?? new PublicParserObservedLogisticsSnapshot(nowUtc);

        if (_dbContext.Entry(snapshot).State == EntityState.Detached)
            _dbContext.PublicParserObservedLogisticsSnapshots.Add(snapshot);

        snapshot.MarkCompleted(
            eventResponse.CurrentLogisticsRunId,
            eventResponse.PreviousLogisticsRunId,
            eventResponse.CurrentObservedAtUtc,
            eventResponse.PreviousObservedAtUtc,
            JsonSerializer.Serialize(eventResponse.Items, JsonOptions),
            JsonSerializer.Serialize(eventResponse.Summary, JsonOptions),
            JsonSerializer.Serialize(stockDecreaseResponse.Items, JsonOptions),
            JsonSerializer.Serialize(stockDecreaseResponse.Summary, JsonOptions),
            eventResponse.TotalCount,
            stockDecreaseResponse.TotalCount,
            nowUtc,
            (long)stopwatch.Elapsed.TotalMilliseconds);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PublicParserObservedLogisticsRefreshResult>.Success(
            new PublicParserObservedLogisticsRefreshResult(eventResponse.TotalCount, stockDecreaseResponse.TotalCount, nowUtc));
    }

    private async Task<ParserObservedMarketEventResponse> BuildMarketEventsAsync(CancellationToken cancellationToken)
    {
        var latestLogisticsRunId = await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.Kind == "logistics" && x.ManifestStatus == "succeeded")
            .OrderByDescending(x => x.FinishedAtUtc.HasValue)
            .ThenByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.DateRegisteredUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .Select(x => x.ParserRunId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestLogisticsRunId is null)
            return EmptyMarketEventResponse();

        var changedPairs = await _dbContext.Database.SqlQueryRaw<LogisticsObservationPair>(
            """
            WITH ranked AS (
                SELECT
                    parser_run_id,
                    wb_product_id,
                    source_region_dest,
                    total_quantity_observed,
                    observed_at_utc,
                    source_line_number,
                    ROW_NUMBER() OVER (
                        PARTITION BY wb_product_id, source_region_dest
                        ORDER BY observed_at_utc DESC, source_line_number DESC
                    ) AS rn
                FROM "ParserLogisticsSnapshotRows"
            )
            SELECT
                current.parser_run_id AS "CurrentParserRunId",
                previous.parser_run_id AS "PreviousParserRunId",
                current.wb_product_id AS "WbProductId",
                current.source_region_dest AS "Destination",
                previous.total_quantity_observed AS "PreviousQuantity",
                current.total_quantity_observed AS "CurrentQuantity",
                previous.observed_at_utc AS "PreviousObservedAtUtc",
                current.observed_at_utc AS "CurrentObservedAtUtc"
            FROM ranked current
            JOIN ranked previous
                ON previous.wb_product_id = current.wb_product_id
                AND previous.source_region_dest = current.source_region_dest
                AND previous.rn = 2
            WHERE current.rn = 1
                AND current.total_quantity_observed IS NOT NULL
                AND previous.total_quantity_observed IS NOT NULL
                AND current.total_quantity_observed <> previous.total_quantity_observed;
            """)
            .ToListAsync(cancellationToken);

        var newProducts = await _dbContext.Database.SqlQueryRaw<CurrentOnlyLogisticsObservation>(
            """
            SELECT
                current.parser_run_id AS "CurrentParserRunId",
                current.wb_product_id AS "WbProductId",
                current.source_region_dest AS "Destination",
                current.total_quantity_observed AS "CurrentQuantity",
                current.observed_at_utc AS "CurrentObservedAtUtc"
            FROM "ParserLogisticsSnapshotRows" current
            WHERE current.parser_run_id = {0}
                AND NOT EXISTS (
                    SELECT 1
                    FROM "ParserLogisticsSnapshotRows" previous
                    WHERE previous.wb_product_id = current.wb_product_id
                        AND previous.source_region_dest = current.source_region_dest
                        AND (
                            previous.observed_at_utc < current.observed_at_utc
                            OR (
                                previous.observed_at_utc = current.observed_at_utc
                                AND previous.source_line_number < current.source_line_number
                            )
                        )
                );
            """,
            latestLogisticsRunId)
            .ToListAsync(cancellationToken);

        var metadata = await LoadProductMetadataAsync(
            changedPairs.Select(x => x.WbProductId).Concat(newProducts.Select(x => x.WbProductId)),
            cancellationToken);

        var changedItems = changedPairs
            .Select(pair => MapChangedPair(pair, metadata))
            .ToList();

        var newProductItems = newProducts
            .Select(item => MapCurrentOnly(item, metadata))
            .ToList();

        var items = changedItems
            .Concat(newProductItems)
            .GroupBy(x => $"{x.EventType}:{x.WbProductId}", StringComparer.Ordinal)
            .Select(SelectProductRepresentative)
            .OrderBy(x => EventPriority(x.EventType))
            .ThenByDescending(x => Math.Abs(x.QuantityChange ?? 0))
            .ThenByDescending(x => x.CurrentObservedAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToList();

        var currentObservedAtUtc = changedPairs
            .Select(x => (DateTime?)x.CurrentObservedAtUtc)
            .Concat(newProducts.Select(x => (DateTime?)x.CurrentObservedAtUtc))
            .Max();
        var previousObservedAtUtc = changedPairs.Select(x => (DateTime?)x.PreviousObservedAtUtc).Max();

        return new ParserObservedMarketEventResponse(
            latestLogisticsRunId,
            "latest_previous_by_product_destination",
            currentObservedAtUtc,
            previousObservedAtUtc,
            items.Count,
            1,
            items.Count,
            items,
            BuildMarketEventSummary(items),
            []);
    }

    private async Task<IReadOnlyDictionary<string, ProductMetadata>> LoadProductMetadataAsync(
        IEnumerable<string> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (ids.Count == 0)
            return new Dictionary<string, ProductMetadata>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserCurrentProductRows
            .AsNoTracking()
            .Where(x => ids.Contains(x.WbProductId))
            .Select(x => new ProductMetadata(
                x.ProductRowId,
                x.WbProductId,
                x.WbRootId,
                x.Name,
                x.BrandName,
                x.SellerName,
                x.SourceCategory,
                x.SourceSubcategory,
                x.PriceRegular,
                x.PriceDiscounted,
                x.PriceWbWallet,
                x.ReviewRating,
                x.FeedbackCount))
            .ToListAsync(cancellationToken);

        var imageUrls = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => rows.Select(row => row.ProductRowId).Contains(x.Id))
            .Select(x => new { x.Id, x.ImageUrls })
            .ToDictionaryAsync(x => x.Id, x => FirstImageUrl(x.ImageUrls), cancellationToken);

        return rows
            .Select(x => x with { ImageUrl = imageUrls.GetValueOrDefault(x.ProductRowId) })
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
    }

    private static ParserObservedMarketEventItemDto MapChangedPair(
        LogisticsObservationPair pair,
        IReadOnlyDictionary<string, ProductMetadata> metadataByProductId)
    {
        metadataByProductId.TryGetValue(pair.WbProductId, out var metadata);
        var quantityChange = pair.CurrentQuantity!.Value - pair.PreviousQuantity!.Value;
        return new ParserObservedMarketEventItemDto(
            quantityChange < 0 ? ParserObservedMarketEventTypes.StockDecreased : ParserObservedMarketEventTypes.StockIncreased,
            metadata?.ProductRowId,
            pair.WbProductId,
            metadata?.WbRootId,
            metadata?.Name,
            metadata?.BrandName,
            metadata?.SellerName,
            metadata?.SourceCategory,
            metadata?.SourceSubcategory,
            pair.Destination,
            pair.PreviousQuantity,
            pair.CurrentQuantity,
            quantityChange,
            pair.PreviousObservedAtUtc,
            pair.CurrentObservedAtUtc,
            SelectedPrice(metadata),
            metadata?.PriceRegular,
            metadata?.PriceDiscounted,
            metadata?.PriceWbWallet,
            metadata?.Rating,
            metadata?.FeedbackCount,
            metadata?.ImageUrl);
    }

    private static ParserObservedMarketEventItemDto MapCurrentOnly(
        CurrentOnlyLogisticsObservation item,
        IReadOnlyDictionary<string, ProductMetadata> metadataByProductId)
    {
        metadataByProductId.TryGetValue(item.WbProductId, out var metadata);
        return new ParserObservedMarketEventItemDto(
            ParserObservedMarketEventTypes.NewProductObserved,
            metadata?.ProductRowId,
            item.WbProductId,
            metadata?.WbRootId,
            metadata?.Name,
            metadata?.BrandName,
            metadata?.SellerName,
            metadata?.SourceCategory,
            metadata?.SourceSubcategory,
            item.Destination,
            null,
            item.CurrentQuantity,
            null,
            null,
            item.CurrentObservedAtUtc,
            SelectedPrice(metadata),
            metadata?.PriceRegular,
            metadata?.PriceDiscounted,
            metadata?.PriceWbWallet,
            metadata?.Rating,
            metadata?.FeedbackCount,
            metadata?.ImageUrl);
    }

    private static ParserObservedMarketEventItemDto SelectProductRepresentative(
        IEnumerable<ParserObservedMarketEventItemDto> productEvents)
    {
        return productEvents
            .OrderByDescending(x => Math.Abs(x.QuantityChange ?? 0))
            .ThenByDescending(x => x.CurrentQuantity ?? 0)
            .ThenByDescending(x => x.CurrentObservedAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.Destination, StringComparer.Ordinal)
            .First();
    }

    private static ParserObservedMarketEventSummaryDto BuildMarketEventSummary(
        IReadOnlyList<ParserObservedMarketEventItemDto> items)
    {
        var decreases = items.Where(x => x.EventType == ParserObservedMarketEventTypes.StockDecreased).ToList();
        var increases = items.Where(x => x.EventType == ParserObservedMarketEventTypes.StockIncreased).ToList();
        var newProducts = items.Count(x => x.EventType == ParserObservedMarketEventTypes.NewProductObserved);
        return new ParserObservedMarketEventSummaryDto(
            items.Count,
            decreases.Count,
            increases.Count,
            newProducts,
            0,
            0,
            0,
            newProducts,
            0,
            decreases.Count + increases.Count,
            decreases.Sum(x => Math.Abs(x.QuantityChange ?? 0)),
            increases.Sum(x => Math.Abs(x.QuantityChange ?? 0)));
    }

    private static ParserObservedStockDecreaseResponse BuildStockDecreaseResponse(ParserObservedMarketEventResponse eventResponse)
    {
        var items = eventResponse.Items
            .Where(x =>
                x.EventType == ParserObservedMarketEventTypes.StockDecreased
                && x.PreviousQuantity.HasValue
                && x.CurrentQuantity.HasValue
                && x.QuantityChange.HasValue)
            .Select(x => new ParserObservedStockDecreaseItemDto(
                x.WbProductId,
                x.WbRootId,
                x.Name,
                x.BrandName,
                x.SellerName,
                x.SourceCategory,
                x.SourceSubcategory,
                x.Destination,
                x.PreviousQuantity!.Value,
                x.CurrentQuantity!.Value,
                Math.Abs(x.QuantityChange!.Value),
                x.PreviousObservedAtUtc!.Value,
                x.CurrentObservedAtUtc!.Value,
                x.Price,
                x.Rating,
                x.FeedbackCount,
                x.ImageUrl))
            .OrderByDescending(x => x.QuantityDecrease)
            .ThenByDescending(x => x.CurrentObservedAtUtc)
            .ToList();

        return new ParserObservedStockDecreaseResponse(
            eventResponse.CurrentLogisticsRunId,
            eventResponse.PreviousLogisticsRunId,
            eventResponse.CurrentObservedAtUtc,
            eventResponse.PreviousObservedAtUtc,
            items.Count,
            1,
            items.Count,
            items,
            BuildStockDecreaseSummary(eventResponse, items),
            []);
    }

    private static ParserObservedStockDecreaseSummaryDto BuildStockDecreaseSummary(
        ParserObservedMarketEventResponse eventResponse,
        IReadOnlyList<ParserObservedStockDecreaseItemDto> items)
    {
        var totalDecrease = items.Sum(x => x.QuantityDecrease);
        return new ParserObservedStockDecreaseSummaryDto(
            eventResponse.Summary.OverlappingProductsCount,
            items.Count,
            0,
            eventResponse.Summary.CurrentOnlyProductsCount,
            0,
            eventResponse.Summary.CurrentOnlyProductsCount,
            totalDecrease,
            items.Count == 0 ? null : items.Max(x => x.QuantityDecrease),
            items.Count == 0 ? null : Math.Round((decimal)totalDecrease / items.Count, 2));
    }

    private static ParserObservedMarketEventResponse EmptyMarketEventResponse() =>
        new(null, null, null, null, 0, 1, 0, [], new ParserObservedMarketEventSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), []);

    private static int EventPriority(string eventType) =>
        eventType switch
        {
            ParserObservedMarketEventTypes.StockDecreased => 0,
            ParserObservedMarketEventTypes.StockIncreased => 1,
            ParserObservedMarketEventTypes.NewProductObserved => 2,
            _ => 3
        };

    private static decimal? SelectedPrice(ProductMetadata? metadata) =>
        metadata?.PriceWbWallet
        ?? metadata?.PriceDiscounted
        ?? metadata?.PriceRegular;

    private static string? FirstImageUrl(JsonDocument? imageUrls)
    {
        if (imageUrls is null || imageUrls.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        return imageUrls.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private sealed class LogisticsObservationPair
    {
        public string CurrentParserRunId { get; init; } = string.Empty;
        public string PreviousParserRunId { get; init; } = string.Empty;
        public string WbProductId { get; init; } = string.Empty;
        public string Destination { get; init; } = string.Empty;
        public int? PreviousQuantity { get; init; }
        public int? CurrentQuantity { get; init; }
        public DateTime PreviousObservedAtUtc { get; init; }
        public DateTime CurrentObservedAtUtc { get; init; }
    }

    private sealed class CurrentOnlyLogisticsObservation
    {
        public string CurrentParserRunId { get; init; } = string.Empty;
        public string WbProductId { get; init; } = string.Empty;
        public string Destination { get; init; } = string.Empty;
        public int? CurrentQuantity { get; init; }
        public DateTime CurrentObservedAtUtc { get; init; }
    }

    private sealed record ProductMetadata(
        Guid ProductRowId,
        string WbProductId,
        string? WbRootId,
        string Name,
        string? BrandName,
        string? SellerName,
        string? SourceCategory,
        string? SourceSubcategory,
        decimal? PriceRegular,
        decimal? PriceDiscounted,
        decimal? PriceWbWallet,
        decimal? Rating,
        int? FeedbackCount)
    {
        public string? ImageUrl { get; init; }
    }
}
