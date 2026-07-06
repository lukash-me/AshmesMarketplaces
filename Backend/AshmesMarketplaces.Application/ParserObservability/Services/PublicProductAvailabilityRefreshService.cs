using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class PublicProductAvailabilityRefreshService : IPublicProductAvailabilityRefreshService
{
    private const string PositionStateUnknown = "unknown";
    private const string MoscowDeliveryDestination = "1259570991";
    private const string MoscowDeliveryCity = "Москва";
    private const string MoscowDeliveryLabel = "Москва, ПВЗ WB на улице Зацепа 32";
    private const string MoscowDeliveryAddress = "г Москва, улица Зацепа 32";

    private static readonly int[] WbBasketEnds =
    [
        143, 287, 431, 719, 1007, 1061, 1115, 1169, 1313, 1601, 1655, 1919,
        2045, 2189, 2405, 2621, 2837, 3053, 3269, 3485, 3701, 3917, 4133,
        4349, 4565, 4877, 5189, 5501, 5813, 6125, 6437, 6749, 7061, 7373,
        7685, 7997, 8309, 8741, 9173, 9605, 10373, 11141, 11909, 12677,
        13445, 14213
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public PublicProductAvailabilityRefreshService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicProductAvailabilityRefreshResult>> RefreshAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var items = await LoadAvailableCurrentProductsAsync(cancellationToken);

        stopwatch.Stop();
        var nowUtc = DateTime.UtcNow;
        var snapshot = await _dbContext.PublicProductAvailabilitySnapshots
            .FirstOrDefaultAsync(x => x.SnapshotKey == PublicProductAvailabilitySnapshot.SnapshotKeyValue, cancellationToken)
            ?? new PublicProductAvailabilitySnapshot(nowUtc);

        if (_dbContext.Entry(snapshot).State == EntityState.Detached)
            _dbContext.PublicProductAvailabilitySnapshots.Add(snapshot);

        snapshot.MarkCompleted(
            JsonSerializer.Serialize(items, JsonOptions),
            items.Count,
            nowUtc,
            (long)stopwatch.Elapsed.TotalMilliseconds);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PublicProductAvailabilityRefreshResult>.Success(
            new PublicProductAvailabilityRefreshResult(items.Count, nowUtc));
    }

    private async Task<List<ParserProductListItemDto>> LoadAvailableCurrentProductsAsync(CancellationToken cancellationToken)
    {
        var currentProducts = await _dbContext.ParserCurrentProductRows
            .AsNoTracking()
            .OrderByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
        if (currentProducts.Count == 0)
            return [];

        var productIds = currentProducts
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (productIds.Count == 0)
            return [];

        var latestLogisticsByProductId = await LoadLatestLogisticsByProductIdAsync(productIds, cancellationToken);
        if (latestLogisticsByProductId.Count == 0)
            return [];

        var sourceImageUrlsByProductId = await LoadSourceImageUrlsByProductIdAsync(productIds, cancellationToken);
        var warehouseCounts = await LoadWarehouseCountsAsync(latestLogisticsByProductId.Values, cancellationToken);
        var reviewEvidenceByProductId = await LoadReviewEvidenceByProductIdAsync(productIds, cancellationToken);

        return currentProducts
            .Where(product => latestLogisticsByProductId.ContainsKey(product.WbProductId))
            .Select(product =>
            {
                var logistics = latestLogisticsByProductId[product.WbProductId];
                var warehouseKey = new WarehouseCountKey(logistics.ParserRunId, logistics.WbProductId, logistics.SourceRegionDest);
                warehouseCounts.TryGetValue(warehouseKey, out var warehouseCount);
                reviewEvidenceByProductId.TryGetValue(product.WbProductId, out var reviewEvidence);

                return MapToListItem(
                    product,
                    MapLogisticsSummary(logistics, warehouseCount, warehouseCount > 0),
                    reviewEvidence ?? EmptyReviewEvidence(product),
                    sourceImageUrlsByProductId.GetValueOrDefault(product.WbProductId));
            })
            .ToList();
    }

    private async Task<Dictionary<string, string?>> LoadSourceImageUrlsByProductIdAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return [];

        var rows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(row => row.ParsedAtUtc)
                    .ThenByDescending(row => row.SourceLineNumber)
                    .Select(row => row.ImageUrls?.RootElement.GetRawText())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, ParserLogisticsSnapshotRow>> LoadLatestLogisticsByProductIdAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken)
    {
        var logisticsRows = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.WbProductId)
                && x.DeliveryProfileKey != null
                && x.DeliveryProfileKey != "")
            .ToListAsync(cancellationToken);

        return logisticsRows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(row => row.ObservedAtUtc)
                    .ThenByDescending(row => row.SourceLineNumber)
                    .First(),
                StringComparer.Ordinal);
    }

    private async Task<Dictionary<WarehouseCountKey, int>> LoadWarehouseCountsAsync(
        IEnumerable<ParserLogisticsSnapshotRow> selectedLogisticsRows,
        CancellationToken cancellationToken)
    {
        var keys = selectedLogisticsRows
            .Select(x => new WarehouseCountKey(x.ParserRunId, x.WbProductId, x.SourceRegionDest))
            .Distinct()
            .ToList();
        if (keys.Count == 0)
            return [];

        var parserRunIds = keys.Select(x => x.ParserRunId).Distinct(StringComparer.Ordinal).ToList();
        var productIds = keys.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var destinationIds = keys.Select(x => x.SourceRegionDest).Distinct(StringComparer.Ordinal).ToList();

        var rows = await _dbContext.ParserWarehouseAvailabilityRows
            .AsNoTracking()
            .Where(x =>
                parserRunIds.Contains(x.ParserRunId)
                && productIds.Contains(x.WbProductId)
                && destinationIds.Contains(x.SourceRegionDest))
            .Select(x => new
            {
                x.ParserRunId,
                x.WbProductId,
                x.SourceRegionDest,
                x.WarehouseIdOnMp
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new WarehouseCountKey(x.ParserRunId, x.WbProductId, x.SourceRegionDest))
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(row => row.WarehouseIdOnMp)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .Count());
    }

    private async Task<Dictionary<string, ParserProductReviewEvidenceDto>> LoadReviewEvidenceByProductIdAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken)
    {
        var summaries = await _dbContext.ParserCurrentProductReviewsSummaries
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId))
            .ToListAsync(cancellationToken);

        return summaries.ToDictionary(
            x => x.WbProductId,
            x => new ParserProductReviewEvidenceDto(
                RootFetchCount: 0,
                ParsedReviewCount: x.ReviewsCount,
                ParsedReplyCount: 0,
                LatestReviewRunId: x.BatchId,
                AttributionMode: "current_summary",
                IsRootScoped: false,
                IsFullHistoryUnknown: !string.Equals(x.CoverageStatus, "full", StringComparison.OrdinalIgnoreCase),
                HasCappedRootPayload: string.Equals(x.CoverageSource, "root_capped_fallback", StringComparison.OrdinalIgnoreCase),
                MarketplaceFeedbackCount: x.MarketplaceFeedbackCount,
                FetchedReviewsCount: x.FetchedReviewsCount,
                OldestReviewDateUtc: x.OldestReviewDateUtc,
                LatestReviewDateUtc: x.LastReviewDateUtc,
                CoverageStatus: x.CoverageStatus,
                CoverageSource: x.CoverageSource,
                LastCoverageError: x.LastCoverageError),
            StringComparer.Ordinal);
    }

    private static ParserProductListItemDto MapToListItem(
        ParserCurrentProductRow row,
        ParserProductLogisticsSummaryDto logistics,
        ParserProductReviewEvidenceDto reviewEvidence,
        string? sourceImageUrlsJson)
    {
        return new ParserProductListItemDto(
            row.ProductRowId,
            row.ParserRunId,
            row.ParsedAtUtc,
            row.WbProductId,
            row.WbRootId,
            row.Name,
            row.BrandName,
            row.SellerName,
            row.PriceRegular,
            row.PriceDiscounted,
            row.PriceWbWallet,
            ToNullableInt(row.DiscountPercent),
            row.TotalQuantity,
            ToNullableInt(row.RatingRounded),
            row.ReviewRating,
            row.FeedbackCount,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            FirstImageUrl(row.ImageUrlsJson, sourceImageUrlsJson) ?? BuildWbImageUrl(row.WbProductId),
            null,
            MapPosition(row),
            logistics,
            reviewEvidence);
    }

    private static ParserProductPositionDto MapPosition(ParserCurrentProductRow row)
    {
        return new ParserProductPositionDto(
            string.IsNullOrWhiteSpace(row.PositionState) ? PositionStateUnknown : row.PositionState,
            row.PositionAbsolute,
            row.PositionObservedRangeLimit,
            row.PositionQuery ?? row.SourceQuery,
            row.SourceCategory,
            row.SourceSubcategory,
            row.PositionObservedAtUtc);
    }

    private static ParserProductLogisticsSummaryDto MapLogisticsSummary(
        ParserLogisticsSnapshotRow row,
        int warehouseCount,
        bool hasWarehouseRows)
    {
        return new ParserProductLogisticsSummaryDto(
            row.TotalQuantityObserved,
            row.QuantityIsCapped,
            row.QuantityCapObserved,
            QuantityLabel(row.TotalQuantityObserved, row.QuantityIsCapped, row.QuantityCapObserved),
            row.QuantitySemantics,
            warehouseCount,
            row.SourceRegionDest,
            row.DeliveryProfileKey,
            row.DeliveryDestinationName,
            row.DeliveryProfileVersion,
            DeliveryDestinationCity(row.SourceRegionDest, row.DeliveryDestinationCity, row.DeliveryDestinationName),
            DeliveryDestinationLabel(row.SourceRegionDest, row.DeliveryDestinationLabel),
            DeliveryDestinationAddress(row.SourceRegionDest, row.DeliveryDestinationAddress),
            row.DeliveryDestinationLatitude,
            row.DeliveryDestinationLongitude,
            row.ParserRunId,
            row.ObservedAtUtc,
            hasWarehouseRows);
    }

    private static ParserProductReviewEvidenceDto EmptyReviewEvidence(ParserCurrentProductRow row) =>
        new(
            RootFetchCount: 0,
            ParsedReviewCount: 0,
            ParsedReplyCount: 0,
            LatestReviewRunId: null,
            AttributionMode: "none",
            IsRootScoped: false,
            IsFullHistoryUnknown: true,
            HasCappedRootPayload: false,
            MarketplaceFeedbackCount: row.FeedbackCount,
            FetchedReviewsCount: 0,
            OldestReviewDateUtc: null,
            LatestReviewDateUtc: null,
            CoverageStatus: "unknown",
            CoverageSource: "none",
            LastCoverageError: null);

    private static string? DeliveryDestinationCity(string sourceRegionDest, string? city, string? destinationName)
    {
        if (!string.IsNullOrWhiteSpace(city))
            return city;
        if (!string.IsNullOrWhiteSpace(destinationName))
            return destinationName;
        return sourceRegionDest == MoscowDeliveryDestination ? MoscowDeliveryCity : null;
    }

    private static string? DeliveryDestinationLabel(string sourceRegionDest, string? label)
    {
        if (!string.IsNullOrWhiteSpace(label))
            return label;
        return sourceRegionDest == MoscowDeliveryDestination ? MoscowDeliveryLabel : null;
    }

    private static string? DeliveryDestinationAddress(string sourceRegionDest, string? address)
    {
        if (!string.IsNullOrWhiteSpace(address))
            return address;
        return sourceRegionDest == MoscowDeliveryDestination ? MoscowDeliveryAddress : null;
    }

    private static string QuantityLabel(int? totalQuantityObserved, bool? quantityIsCapped, int? quantityCapObserved)
    {
        if (quantityIsCapped == true && quantityCapObserved.HasValue)
            return $"≥{quantityCapObserved.Value.ToString(CultureInfo.InvariantCulture)}";

        return totalQuantityObserved.HasValue
            ? totalQuantityObserved.Value.ToString(CultureInfo.InvariantCulture)
            : "Нет данных";
    }

    private static IReadOnlyList<string> GetImageUrls(string? imageUrlsJson)
    {
        if (string.IsNullOrWhiteSpace(imageUrlsJson))
            return [];

        using var document = JsonDocument.Parse(imageUrlsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return document.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    private static int? ToNullableInt(decimal? value) =>
        value.HasValue ? decimal.ToInt32(value.Value) : null;

    private static string? FirstImageUrl(params string?[] imageUrlsJsonValues) =>
        imageUrlsJsonValues
            .SelectMany(GetImageUrls)
            .FirstOrDefault();

    private static string? BuildWbImageUrl(string wbProductId)
    {
        if (!long.TryParse(wbProductId, out var productId) || productId <= 0)
            return null;

        var volume = productId / 100000;
        var part = productId / 1000;
        var basket = CalculateWbBasket(volume);
        return $"https://basket-{basket}.wbbasket.ru/vol{volume}/part{part}/{productId}/images/big/1.webp";
    }

    private static string CalculateWbBasket(long volume)
    {
        var index = Array.BinarySearch(WbBasketEnds, (int)Math.Min(volume, int.MaxValue));
        if (index < 0)
            index = ~index;
        else
            index += 1;

        return (index + 1).ToString("00", CultureInfo.InvariantCulture);
    }

    private sealed record WarehouseCountKey(string ParserRunId, string WbProductId, string SourceRegionDest);
}
