using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class ParserObservedMarketEventReadService : IParserObservedMarketEventReadService
{
    private const string LogisticsKind = "logistics";
    private const string ProductsKind = "products";
    private const string SucceededStatus = "succeeded";
    private const string WarningObservedEventsAreNotConfirmedOrders = "observed_events_are_not_confirmed_orders";
    private const string WarningQuantityExactnessNotProven = "quantity_exactness_not_proven";
    private const string WarningStockCapsMayHideEvents = "stock_caps_may_hide_events";
    private const string WarningLogisticsRunCoverageMayDiffer = "logistics_run_coverage_may_differ";
    private const string WarningNotEnoughLogisticsRuns = "not_enough_logistics_runs";

    private readonly ApplicationDbContext _dbContext;

    public ParserObservedMarketEventReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ParserObservedMarketEventResponse>> GetListAsync(
        ParserObservedMarketEventQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var minQuantityChange = Math.Max(query.MinQuantityChange ?? 1, 1);
        var sort = query.Sort?.Trim();
        var eventType = query.EventType?.Trim();

        var runResolution = await ResolveRunPairAsync(query, cancellationToken);
        if (!string.IsNullOrWhiteSpace(runResolution.ErrorMessage))
            return ServiceResult<ParserObservedMarketEventResponse>.BadRequest(runResolution.ErrorMessage);

        if (runResolution.Pair is null)
        {
            return ServiceResult<ParserObservedMarketEventResponse>.Success(
                EmptyResponse(page, pageSize, includeNotEnoughRunsWarning: true));
        }

        var currentSnapshots = await LoadSnapshotsAsync(runResolution.Pair.CurrentParserRunId, cancellationToken);
        var previousSnapshots = await LoadSnapshotsAsync(runResolution.Pair.PreviousParserRunId, cancellationToken);
        var allProductIds = currentSnapshots.Keys
            .Concat(previousSnapshots.Keys)
            .Select(x => x.WbProductId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var productMetadata = await LoadProductMetadataAsync(allProductIds, cancellationToken);

        var records = BuildComparisonRecords(currentSnapshots, previousSnapshots, productMetadata)
            .Where(record => MatchesMetadataFilters(record, query))
            .ToList();
        var summary = BuildSummary(records);
        var events = records
            .Select(CreateEvent)
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => MatchesEventType(x, eventType))
            .Where(x => MatchesMinQuantityChange(x, minQuantityChange))
            .ToList();
        var ordered = ApplySort(events, sort).ToList();
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapItem)
            .ToList();

        var response = new ParserObservedMarketEventResponse(
            runResolution.Pair.CurrentParserRunId,
            runResolution.Pair.PreviousParserRunId,
            MaxObservedAt(currentSnapshots.Values),
            MaxObservedAt(previousSnapshots.Values),
            ordered.Count,
            page,
            pageSize,
            items,
            summary,
            BuildWarnings(includeNotEnoughRunsWarning: false));

        return ServiceResult<ParserObservedMarketEventResponse>.Success(response);
    }

    private async Task<RunPairResolution> ResolveRunPairAsync(
        ParserObservedMarketEventQuery query,
        CancellationToken cancellationToken)
    {
        var hasCurrentRunId = !string.IsNullOrWhiteSpace(query.CurrentLogisticsRunId);
        var hasPreviousRunId = !string.IsNullOrWhiteSpace(query.PreviousLogisticsRunId);
        if (hasCurrentRunId != hasPreviousRunId)
            return RunPairResolution.BadRequest("Current and previous logistics run ids must be provided together.");

        if (hasCurrentRunId && hasPreviousRunId)
        {
            var currentRunId = query.CurrentLogisticsRunId!.Trim();
            var previousRunId = query.PreviousLogisticsRunId!.Trim();
            if (string.Equals(currentRunId, previousRunId, StringComparison.Ordinal))
                return RunPairResolution.BadRequest("Current and previous logistics run ids must be different.");

            var runIds = new[] { currentRunId, previousRunId };
            var validRuns = await _dbContext.ParserRuns
                .AsNoTracking()
                .Where(x =>
                    runIds.Contains(x.ParserRunId)
                    && x.Kind == LogisticsKind
                    && x.ManifestStatus == SucceededStatus)
                .Select(x => x.ParserRunId)
                .ToListAsync(cancellationToken);

            if (validRuns.Count != 2)
                return RunPairResolution.BadRequest("Both run ids must reference successful logistics parser runs.");

            return RunPairResolution.Success(new LogisticsRunPair(currentRunId, previousRunId));
        }

        var runs = await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.Kind == LogisticsKind && x.ManifestStatus == SucceededStatus)
            .OrderByDescending(x => x.FinishedAtUtc.HasValue)
            .ThenByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.DateRegisteredUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .Select(x => x.ParserRunId)
            .Take(2)
            .ToListAsync(cancellationToken);

        return runs.Count < 2
            ? RunPairResolution.NotEnoughRuns()
            : RunPairResolution.Success(new LogisticsRunPair(runs[0], runs[1]));
    }

    private async Task<IReadOnlyDictionary<ObservedMarketEventKey, LogisticsSnapshot>> LoadSnapshotsAsync(
        string parserRunId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x => x.ParserRunId == parserRunId)
            .Select(x => new LogisticsSnapshot(
                x.WbProductId,
                x.SourceRegionDest,
                x.TotalQuantityObserved,
                x.ObservedAtUtc,
                x.SourceLineNumber))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new ObservedMarketEventKey(x.WbProductId, x.Destination))
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(row => row.ObservedAtUtc)
                    .ThenByDescending(row => row.SourceLineNumber)
                    .First());
    }

    private async Task<IReadOnlyDictionary<string, ProductMetadata>> LoadProductMetadataAsync(
        IReadOnlyList<string> wbProductIds,
        CancellationToken cancellationToken)
    {
        if (wbProductIds.Count == 0)
            return new Dictionary<string, ProductMetadata>(StringComparer.Ordinal);

        var latestProductRunId = await ResolveLatestParserRunIdAsync(ProductsKind, cancellationToken);
        if (latestProductRunId is null)
            return new Dictionary<string, ProductMetadata>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.ParserRunId == latestProductRunId && wbProductIds.Contains(x.WbProductId))
            .Select(x => new ProductMetadata(
                x.Id,
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
                x.RatingRounded,
                x.ReviewRating,
                x.FeedbackCount,
                x.ImageUrls,
                x.ParsedAtUtc,
                x.SourceLineNumber))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(row => row.ParsedAtUtc)
                    .ThenByDescending(row => row.SourceLineNumber)
                    .First(),
                StringComparer.Ordinal);
    }

    private async Task<string?> ResolveLatestParserRunIdAsync(
        string kind,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.Kind == kind && x.ManifestStatus == SucceededStatus)
            .OrderByDescending(x => x.FinishedAtUtc.HasValue)
            .ThenByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.DateRegisteredUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .Select(x => x.ParserRunId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IEnumerable<ComparisonRecord> BuildComparisonRecords(
        IReadOnlyDictionary<ObservedMarketEventKey, LogisticsSnapshot> currentSnapshots,
        IReadOnlyDictionary<ObservedMarketEventKey, LogisticsSnapshot> previousSnapshots,
        IReadOnlyDictionary<string, ProductMetadata> productMetadata)
    {
        var keys = currentSnapshots.Keys
            .Concat(previousSnapshots.Keys)
            .Distinct()
            .OrderBy(x => x.WbProductId, StringComparer.Ordinal)
            .ThenBy(x => x.Destination, StringComparer.Ordinal);

        foreach (var key in keys)
        {
            currentSnapshots.TryGetValue(key, out var current);
            previousSnapshots.TryGetValue(key, out var previous);
            productMetadata.TryGetValue(key.WbProductId, out var metadata);
            yield return new ComparisonRecord(key, current, previous, metadata);
        }
    }

    private static bool MatchesMetadataFilters(
        ComparisonRecord record,
        ParserObservedMarketEventQuery query)
    {
        var metadata = record.Metadata;
        if (!MatchesExact(metadata?.SourceCategory, query.SourceCategory))
            return false;

        if (!MatchesExact(metadata?.SourceSubcategory, query.SourceSubcategory))
            return false;

        if (!MatchesExact(metadata?.BrandName, query.BrandName))
            return false;

        if (!MatchesExact(metadata?.SellerName, query.SellerName))
            return false;

        if (string.IsNullOrWhiteSpace(query.Search))
            return true;

        var search = query.Search.Trim();
        return Contains(record.Key.WbProductId, search)
            || Contains(metadata?.WbRootId, search)
            || Contains(metadata?.Name, search)
            || Contains(metadata?.BrandName, search)
            || Contains(metadata?.SellerName, search);
    }

    private static ObservedMarketEventRecord? CreateEvent(ComparisonRecord record)
    {
        if (record.Current is not null && record.Previous is null)
        {
            return new ObservedMarketEventRecord(
                record,
                ParserObservedMarketEventTypes.NewProductObserved,
                QuantityChange: null);
        }

        if (record.Current is null && record.Previous is not null)
        {
            return new ObservedMarketEventRecord(
                record,
                ParserObservedMarketEventTypes.ProductMissingInCurrent,
                QuantityChange: null);
        }

        if (record.Current is null || record.Previous is null)
            return null;

        if (!record.Current.TotalQuantityObserved.HasValue || !record.Previous.TotalQuantityObserved.HasValue)
            return null;

        var quantityChange = record.Current.TotalQuantityObserved.Value - record.Previous.TotalQuantityObserved.Value;
        return quantityChange switch
        {
            < 0 => new ObservedMarketEventRecord(
                record,
                ParserObservedMarketEventTypes.StockDecreased,
                quantityChange),
            > 0 => new ObservedMarketEventRecord(
                record,
                ParserObservedMarketEventTypes.StockIncreased,
                quantityChange),
            _ => null
        };
    }

    private static bool MatchesEventType(ObservedMarketEventRecord record, string? eventType)
    {
        return string.IsNullOrWhiteSpace(eventType)
            || string.Equals(record.EventType, eventType, StringComparison.Ordinal);
    }

    private static bool MatchesMinQuantityChange(ObservedMarketEventRecord record, int minQuantityChange)
    {
        return record.EventType is not ParserObservedMarketEventTypes.StockDecreased
                and not ParserObservedMarketEventTypes.StockIncreased
            || Math.Abs(record.QuantityChange!.Value) >= minQuantityChange;
    }

    private static ParserObservedMarketEventSummaryDto BuildSummary(IReadOnlyList<ComparisonRecord> records)
    {
        var currentOnly = 0;
        var previousOnly = 0;
        var overlapping = 0;
        var stockDecreased = 0;
        var stockIncreased = 0;
        var unchanged = 0;
        var notComparable = 0;
        var totalObservedDecrease = 0;
        var totalObservedIncrease = 0;

        foreach (var record in records)
        {
            if (record.Current is not null && record.Previous is null)
            {
                currentOnly++;
                continue;
            }

            if (record.Current is null && record.Previous is not null)
            {
                previousOnly++;
                continue;
            }

            if (record.Current is null || record.Previous is null)
                continue;

            overlapping++;
            if (!record.Current.TotalQuantityObserved.HasValue || !record.Previous.TotalQuantityObserved.HasValue)
            {
                notComparable++;
                continue;
            }

            var quantityChange = record.Current.TotalQuantityObserved.Value - record.Previous.TotalQuantityObserved.Value;
            if (quantityChange < 0)
            {
                stockDecreased++;
                totalObservedDecrease += Math.Abs(quantityChange);
            }
            else if (quantityChange > 0)
            {
                stockIncreased++;
                totalObservedIncrease += quantityChange;
            }
            else
            {
                unchanged++;
            }
        }

        return new ParserObservedMarketEventSummaryDto(
            records.Count,
            stockDecreased,
            stockIncreased,
            currentOnly,
            previousOnly,
            unchanged,
            notComparable,
            currentOnly,
            previousOnly,
            overlapping,
            totalObservedDecrease,
            totalObservedIncrease);
    }

    private static IOrderedEnumerable<ObservedMarketEventRecord> ApplySort(
        IEnumerable<ObservedMarketEventRecord> records,
        string? sort)
    {
        return sort switch
        {
            "eventType" => records
                .OrderBy(x => x.EventType, StringComparer.Ordinal)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-eventType" => records
                .OrderByDescending(x => x.EventType, StringComparer.Ordinal)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "quantityChange" => records
                .OrderBy(x => x.QuantityChange)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-quantityChange" => records
                .OrderByDescending(x => x.QuantityChange)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "currentQuantity" => records
                .OrderBy(x => x.Record.Current?.TotalQuantityObserved)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-currentQuantity" => records
                .OrderByDescending(x => x.Record.Current?.TotalQuantityObserved)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "previousQuantity" => records
                .OrderBy(x => x.Record.Previous?.TotalQuantityObserved)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-previousQuantity" => records
                .OrderByDescending(x => x.Record.Previous?.TotalQuantityObserved)
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "observedAtUtc" => records
                .OrderBy(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-observedAtUtc" => records
                .OrderByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            _ => records
                .OrderBy(x => EventPriority(x.EventType))
                .ThenByDescending(x => ObservedAt(x))
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal)
        };
    }

    private static ParserObservedMarketEventItemDto MapItem(ObservedMarketEventRecord record)
    {
        var comparison = record.Record;
        var metadata = comparison.Metadata;
        return new ParserObservedMarketEventItemDto(
            record.EventType,
            metadata?.ParserProductRowId,
            comparison.Key.WbProductId,
            metadata?.WbRootId,
            metadata?.Name,
            metadata?.BrandName,
            metadata?.SellerName,
            metadata?.SourceCategory,
            metadata?.SourceSubcategory,
            comparison.Key.Destination,
            comparison.Previous?.TotalQuantityObserved,
            comparison.Current?.TotalQuantityObserved,
            record.QuantityChange,
            comparison.Previous?.ObservedAtUtc,
            comparison.Current?.ObservedAtUtc,
            SelectedPrice(metadata),
            metadata?.PriceRegular,
            metadata?.PriceDiscounted,
            metadata?.PriceWbWallet,
            SelectedRating(metadata),
            metadata?.FeedbackCount,
            FirstImageUrl(metadata?.ImageUrls));
    }

    private static ParserObservedMarketEventResponse EmptyResponse(
        int page,
        int pageSize,
        bool includeNotEnoughRunsWarning)
    {
        return new ParserObservedMarketEventResponse(
            null,
            null,
            null,
            null,
            0,
            page,
            pageSize,
            [],
            new ParserObservedMarketEventSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            BuildWarnings(includeNotEnoughRunsWarning));
    }

    private static IReadOnlyList<string> BuildWarnings(bool includeNotEnoughRunsWarning)
    {
        var warnings = new List<string>
        {
            WarningObservedEventsAreNotConfirmedOrders,
            WarningQuantityExactnessNotProven,
            WarningStockCapsMayHideEvents,
            WarningLogisticsRunCoverageMayDiffer
        };

        if (includeNotEnoughRunsWarning)
            warnings.Add(WarningNotEnoughLogisticsRuns);

        return warnings;
    }

    private static bool MatchesExact(string? value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || string.Equals(value?.Trim(), filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? ObservedAt(ObservedMarketEventRecord record)
    {
        return record.Record.Current?.ObservedAtUtc
            ?? record.Record.Previous?.ObservedAtUtc;
    }

    private static int EventPriority(string eventType)
    {
        return eventType switch
        {
            ParserObservedMarketEventTypes.StockDecreased => 0,
            ParserObservedMarketEventTypes.NewProductObserved => 1,
            ParserObservedMarketEventTypes.StockIncreased => 2,
            ParserObservedMarketEventTypes.ProductMissingInCurrent => 3,
            _ => 4
        };
    }

    private static DateTime? MaxObservedAt(IEnumerable<LogisticsSnapshot> snapshots)
    {
        var observedAtValues = snapshots
            .Select(x => x.ObservedAtUtc)
            .ToList();

        return observedAtValues.Count == 0 ? null : observedAtValues.Max();
    }

    private static decimal? SelectedPrice(ProductMetadata? metadata)
    {
        return metadata?.PriceWbWallet
            ?? metadata?.PriceDiscounted
            ?? metadata?.PriceRegular;
    }

    private static decimal? SelectedRating(ProductMetadata? metadata)
    {
        return metadata?.ReviewRating
            ?? metadata?.RatingRounded;
    }

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

    private sealed record RunPairResolution(LogisticsRunPair? Pair, string? ErrorMessage)
    {
        public static RunPairResolution Success(LogisticsRunPair pair) => new(pair, null);

        public static RunPairResolution NotEnoughRuns() => new(null, null);

        public static RunPairResolution BadRequest(string message) => new(null, message);
    }

    private sealed record LogisticsRunPair(
        string CurrentParserRunId,
        string PreviousParserRunId);

    private readonly record struct ObservedMarketEventKey(
        string WbProductId,
        string Destination);

    private sealed record LogisticsSnapshot(
        string WbProductId,
        string Destination,
        int? TotalQuantityObserved,
        DateTime ObservedAtUtc,
        long SourceLineNumber);

    private sealed record ProductMetadata(
        Guid ParserProductRowId,
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
        int? RatingRounded,
        decimal? ReviewRating,
        int? FeedbackCount,
        JsonDocument? ImageUrls,
        DateTime ParsedAtUtc,
        long SourceLineNumber);

    private sealed record ComparisonRecord(
        ObservedMarketEventKey Key,
        LogisticsSnapshot? Current,
        LogisticsSnapshot? Previous,
        ProductMetadata? Metadata);

    private sealed record ObservedMarketEventRecord(
        ComparisonRecord Record,
        string EventType,
        int? QuantityChange);
}
