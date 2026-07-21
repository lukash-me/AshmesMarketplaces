using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class ParserObservedStockDecreaseReadService : IParserObservedStockDecreaseReadService
{
    private const string LogisticsKind = "logistics";
    private const string ProductsKind = "products";
    private const string SucceededStatus = "succeeded";
    private const string WarningObservedDecreaseIsNotConfirmedOrder = "observed_stock_decrease_is_not_confirmed_order";
    private const string WarningQuantityExactnessNotProven = "quantity_exactness_not_proven";
    private const string WarningStockCapsMayHideOrders = "stock_caps_may_hide_orders";
    private const string WarningNotEnoughLogisticsRuns = "not_enough_logistics_runs";

    private readonly ApplicationDbContext _dbContext;

    public ParserObservedStockDecreaseReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ParserObservedStockDecreaseResponse>> GetListAsync(
        ParserObservedStockDecreaseQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var minDecrease = Math.Max(query.MinDecrease ?? 1, 1);
        var sort = string.IsNullOrWhiteSpace(query.Sort) ? "-decrease" : query.Sort.Trim();

        var runResolution = await ResolveRunPairAsync(query, cancellationToken);
        if (!string.IsNullOrWhiteSpace(runResolution.ErrorMessage))
            return ServiceResult<ParserObservedStockDecreaseResponse>.BadRequest(runResolution.ErrorMessage);

        if (runResolution.Pair is null)
        {
            return ServiceResult<ParserObservedStockDecreaseResponse>.Success(
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
            .Where(record => MatchesFilters(record, query))
            .ToList();
        var currentOnlyProductsCount = records.Count(x => x.Current is not null && x.Previous is null);
        var previousOnlyProductsCount = records.Count(x => x.Current is null && x.Previous is not null);
        var overlappingRecords = records
            .Where(x => x.Current is not null && x.Previous is not null)
            .ToList();
        var productsWithoutComparableQuantity = overlappingRecords.Count(x =>
            !x.Current!.TotalQuantityObserved.HasValue || !x.Previous!.TotalQuantityObserved.HasValue);

        var comparableRecords = overlappingRecords
            .Where(x => x.Current!.TotalQuantityObserved.HasValue && x.Previous!.TotalQuantityObserved.HasValue)
            .ToList();
        var decreases = comparableRecords
            .Select(record => new ObservedStockDecreaseRecord(
                record,
                record.Previous!.TotalQuantityObserved!.Value - record.Current!.TotalQuantityObserved!.Value))
            .Where(x => x.QuantityDecrease >= minDecrease)
            .ToList();
        var ordered = ApplySort(decreases, sort).ToList();
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapItem)
            .ToList();

        var response = new ParserObservedStockDecreaseResponse(
            runResolution.Pair.CurrentParserRunId,
            runResolution.Pair.PreviousParserRunId,
            MaxObservedAt(currentSnapshots.Values),
            MaxObservedAt(previousSnapshots.Values),
            ordered.Count,
            page,
            pageSize,
            items,
            BuildSummary(
                comparableRecords.Count,
                ordered,
                productsWithoutComparableQuantity,
                currentOnlyProductsCount,
                previousOnlyProductsCount),
            BuildWarnings(includeNotEnoughRunsWarning: false));

        return ServiceResult<ParserObservedStockDecreaseResponse>.Success(response);
    }

    private async Task<RunPairResolution> ResolveRunPairAsync(
        ParserObservedStockDecreaseQuery query,
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

    private async Task<IReadOnlyDictionary<ObservedStockKey, LogisticsSnapshot>> LoadSnapshotsAsync(
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
            .GroupBy(x => new ObservedStockKey(x.WbProductId, x.Destination))
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
        IReadOnlyDictionary<ObservedStockKey, LogisticsSnapshot> currentSnapshots,
        IReadOnlyDictionary<ObservedStockKey, LogisticsSnapshot> previousSnapshots,
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

    private static bool MatchesFilters(
        ComparisonRecord record,
        ParserObservedStockDecreaseQuery query)
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

    private static IOrderedEnumerable<ObservedStockDecreaseRecord> ApplySort(
        IEnumerable<ObservedStockDecreaseRecord> records,
        string sort)
    {
        return sort switch
        {
            "decrease" => records
                .OrderBy(x => x.QuantityDecrease)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "currentQuantity" => records
                .OrderBy(x => x.Record.Current!.TotalQuantityObserved)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-currentQuantity" => records
                .OrderByDescending(x => x.Record.Current!.TotalQuantityObserved)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "previousQuantity" => records
                .OrderBy(x => x.Record.Previous!.TotalQuantityObserved)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-previousQuantity" => records
                .OrderByDescending(x => x.Record.Previous!.TotalQuantityObserved)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "observedAtUtc" => records
                .OrderBy(x => x.Record.Current!.ObservedAtUtc)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            "-observedAtUtc" => records
                .OrderByDescending(x => x.Record.Current!.ObservedAtUtc)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal),
            _ => records
                .OrderByDescending(x => x.QuantityDecrease)
                .ThenBy(x => x.Record.Key.WbProductId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Key.Destination, StringComparer.Ordinal)
        };
    }

    private static ParserObservedStockDecreaseItemDto MapItem(ObservedStockDecreaseRecord decrease)
    {
        var record = decrease.Record;
        var metadata = record.Metadata;
        return new ParserObservedStockDecreaseItemDto(
            record.Key.WbProductId,
            metadata?.WbRootId,
            metadata?.Name,
            metadata?.BrandName,
            metadata?.SellerName,
            metadata?.SourceCategory,
            metadata?.SourceSubcategory,
            record.Key.Destination,
            record.Previous!.TotalQuantityObserved!.Value,
            record.Current!.TotalQuantityObserved!.Value,
            decrease.QuantityDecrease,
            record.Previous.ObservedAtUtc,
            record.Current.ObservedAtUtc,
            SelectedPrice(metadata),
            SelectedRating(metadata),
            metadata?.FeedbackCount,
            FirstImageUrl(metadata?.ImageUrls));
    }

    private static ParserObservedStockDecreaseSummaryDto BuildSummary(
        int comparedProductsCount,
        IReadOnlyList<ObservedStockDecreaseRecord> decreases,
        int productsWithoutComparableQuantity,
        int currentOnlyProductsCount,
        int previousOnlyProductsCount)
    {
        var totalObservedDecrease = decreases.Sum(x => x.QuantityDecrease);
        return new ParserObservedStockDecreaseSummaryDto(
            comparedProductsCount,
            decreases.Count,
            productsWithoutComparableQuantity,
            currentOnlyProductsCount,
            previousOnlyProductsCount,
            currentOnlyProductsCount + previousOnlyProductsCount,
            totalObservedDecrease,
            decreases.Count == 0 ? null : decreases.Max(x => x.QuantityDecrease),
            decreases.Count == 0 ? null : Math.Round(decreases.Average(x => (decimal)x.QuantityDecrease), 2));
    }

    private static ParserObservedStockDecreaseResponse EmptyResponse(
        int page,
        int pageSize,
        bool includeNotEnoughRunsWarning)
    {
        return new ParserObservedStockDecreaseResponse(
            null,
            null,
            null,
            null,
            0,
            page,
            pageSize,
            [],
            new ParserObservedStockDecreaseSummaryDto(0, 0, 0, 0, 0, 0, 0, null, null),
            BuildWarnings(includeNotEnoughRunsWarning));
    }

    private static IReadOnlyList<string> BuildWarnings(bool includeNotEnoughRunsWarning)
    {
        var warnings = new List<string>
        {
            WarningObservedDecreaseIsNotConfirmedOrder,
            WarningQuantityExactnessNotProven,
            WarningStockCapsMayHideOrders
        };

        if (includeNotEnoughRunsWarning)
            warnings.Add(WarningNotEnoughLogisticsRuns);

        return warnings;
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

    private readonly record struct ObservedStockKey(
        string WbProductId,
        string Destination);

    private sealed record LogisticsSnapshot(
        string WbProductId,
        string Destination,
        int? TotalQuantityObserved,
        DateTime ObservedAtUtc,
        long SourceLineNumber);

    private sealed record ProductMetadata(
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
        ObservedStockKey Key,
        LogisticsSnapshot? Current,
        LogisticsSnapshot? Previous,
        ProductMetadata? Metadata);

    private sealed record ObservedStockDecreaseRecord(
        ComparisonRecord Record,
        int QuantityDecrease);
}
