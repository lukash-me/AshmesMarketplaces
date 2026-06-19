using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicMarketIntelligenceReadService : IPublicMarketIntelligenceReadService
{
    private const string RankKind = "ranks";
    private const string ProductKind = "products";
    private const string SucceededStatus = "succeeded";
    private const string DefaultSort = "popular";
    private const int MaxEvents = 50;
    private const int MaxWeaknesses = 50;
    private const int MaxLeaders = 10;
    private const int MaxHighRankLowStockProducts = 20;
    private const decimal RatingWeaknessDelta = 0.3m;
    private const decimal HighPriceMultiplier = 1.25m;

    private readonly ApplicationDbContext _dbContext;

    public PublicMarketIntelligenceReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicMarketIntelligenceDto>> GetAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        var topN = Math.Clamp(query.TopN, 1, 1000);
        var sort = Normalize(query.Sort) ?? DefaultSort;
        var contextResult = await ResolveContextAsync(query, sort, cancellationToken);
        if (!contextResult.IsSuccess)
            return ServiceResult<PublicMarketIntelligenceDto>.BadRequest(contextResult.Error!);

        var context = contextResult.Value!;
        var latestRankRunId = Normalize(query.LatestRankRunId)
            ?? await ResolveLatestRankRunIdAsync(context, cancellationToken);
        if (latestRankRunId is null)
            return ServiceResult<PublicMarketIntelligenceDto>.NotFound("Наблюдения для выбранного контекста не найдены.");

        var latestRankRows = await LoadRankRowsAsync(latestRankRunId, context, topN, cancellationToken);
        if (latestRankRows.Count == 0)
            return ServiceResult<PublicMarketIntelligenceDto>.NotFound("Наблюдения для выбранного контекста не найдены.");

        var latestCoverage = await LoadCoverageAsync(latestRankRunId, context, topN, latestRankRows, cancellationToken);
        var baselineRankRunId = Normalize(query.BaselineRankRunId)
            ?? await ResolveBaselineRankRunIdAsync(latestRankRunId, context, topN, latestCoverage, cancellationToken);
        var baselineRankRows = baselineRankRunId is null
            ? []
            : await LoadRankRowsAsync(baselineRankRunId, context, topN, cancellationToken);
        var baselineCoverage = baselineRankRunId is null
            ? Coverage.Invalid("baseline_absent", null)
            : await LoadCoverageAsync(baselineRankRunId, context, topN, baselineRankRows, cancellationToken);

        var limitations = new List<string>();
        var windowLimitations = new List<string>();
        if (!latestCoverage.IsFull)
        {
            var limitation = "Проверенный диапазон неполный, часть динамических событий скрыта.";
            limitations.Add(limitation);
            windowLimitations.Add(limitation);
        }

        var isComparable = baselineRankRunId is not null
            && latestCoverage.IsFull
            && baselineCoverage.IsFull
            && IsComparableFingerprint(latestCoverage.RequestFingerprint, baselineCoverage.RequestFingerprint);

        if (!isComparable)
        {
            var limitation = "Недостаточно сопоставимых наблюдений для событий по динамике.";
            limitations.Add(limitation);
            windowLimitations.Add(limitation);
        }

        var latestProductRunId = Normalize(query.LatestProductRunId)
            ?? await ResolveClosestProductRunIdAsync(context, latestRankRows.Max(x => x.ObservedAtUtc), cancellationToken);
        var baselineProductRunId = Normalize(query.BaselineProductRunId)
            ?? (baselineRankRows.Count == 0
                ? null
                : await ResolveClosestProductRunIdAsync(context, baselineRankRows.Max(x => x.ObservedAtUtc), cancellationToken));

        var latestProducts = await LoadProductsAsync(latestProductRunId, context, latestRankRows.Select(x => x.WbProductId), cancellationToken);
        var baselineProducts = await LoadProductsAsync(baselineProductRunId, context, baselineRankRows.Select(x => x.WbProductId), cancellationToken);
        var mapProducts = await LoadLatestProductsForMapAsync(context, cancellationToken);
        var mapProductIds = mapProducts.Select(x => x.WbProductId).ToList();
        var latestDetails = await LoadProductDetailsAsync(mapProductIds, null, cancellationToken);
        var deliveryBuckets = await LoadDeliveryBucketsAsync(context, mapProductIds, cancellationToken);
        var latestRankPositions = latestRankRows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Min(row => row.AbsolutePosition), StringComparer.Ordinal);

        if (latestRankRows.Any(x => !latestProducts.ContainsKey(x.WbProductId)))
            limitations.Add("Для части товаров нет данных карточек в выбранном наблюдении.");

        var latestItems = latestRankRows
            .Select(rank => new RankedProduct(rank, latestProducts.GetValueOrDefault(rank.WbProductId)))
            .ToList();
        var baselineItems = baselineRankRows
            .Select(rank => new RankedProduct(rank, baselineProducts.GetValueOrDefault(rank.WbProductId)))
            .ToList();

        var events = isComparable
            ? BuildEvents(latestItems, baselineItems, baselineProducts, latestProducts, latestRankRows.Max(x => x.ObservedAtUtc))
            : [];
        var competitorWeaknesses = BuildWeaknesses(latestItems);
        var promoPressure = BuildPromoPressure(latestItems, baselineItems, isComparable);
        var pricePressure = BuildPricePressure(latestItems, baselineItems, isComparable);
        var stockPressure = BuildStockPressure(latestItems);
        var concentration = BuildConcentration(latestItems);
        var marketConcentration = MarketIntelligenceConcentrationCalculator.Build(latestItems
            .Select(x => new MarketConcentrationInput(
                x.Rank.WbProductId,
                x.Product?.WbRootId ?? x.Rank.WbRootId,
                x.Product?.SellerName,
                x.Product?.BrandName,
                x.Rank.AbsolutePosition))
            .ToList());
        var priceQualityMap = BuildPriceQualityMap(mapProducts, latestDetails, deliveryBuckets, latestRankPositions);
        var priceCorridors = MarketIntelligencePriceCorridorCalculator.Build(
            priceQualityMap.Points
                .Select(x => new PriceCorridorInput(x.Price, x.Position, x.Rating))
                .ToList());

        var dto = new PublicMarketIntelligenceDto(
            new PublicMarketContextDto(
                context.Marketplace,
                context.SourceCategory,
                context.SourceSubcategory,
                context.Query,
                context.SourceRegionDest,
                context.Sort,
                topN),
            new ObservationWindowDto(
                latestRankRunId,
                baselineRankRunId,
                latestProductRunId,
                baselineProductRunId,
                latestRankRows.Max(x => x.ObservedAtUtc),
                baselineRankRows.Count == 0 ? null : baselineRankRows.Max(x => x.ObservedAtUtc),
                isComparable,
                latestCoverage.IsFull && (baselineRankRunId is null || baselineCoverage.IsFull) ? "complete" : "partial",
                Deduplicate(windowLimitations)),
            events,
            competitorWeaknesses,
            promoPressure,
            pricePressure,
            stockPressure,
            concentration,
            marketConcentration,
            priceQualityMap,
            priceCorridors,
            Deduplicate(limitations));

        return ServiceResult<PublicMarketIntelligenceDto>.Success(dto);
    }

    private async Task<ContextResolution> ResolveContextAsync(
        PublicMarketIntelligenceQuery query,
        string sort,
        CancellationToken cancellationToken)
    {
        var sourceCategory = Normalize(query.SourceCategory);
        var sourceSubcategory = Normalize(query.SourceSubcategory);
        var rankQuery = Normalize(query.Query);
        var sourceRegionDest = Normalize(query.SourceRegionDest);
        var latestRankRunId = Normalize(query.LatestRankRunId);

        if (sourceCategory is not null && sourceSubcategory is not null && rankQuery is not null && sourceRegionDest is not null)
        {
            var marketplace = await _dbContext.ParserRankSnapshotRows
                .AsNoTracking()
                .Where(x =>
                    x.SourceCategory == sourceCategory
                    && x.SourceSubcategory == sourceSubcategory
                    && x.Query == rankQuery
                    && x.SourceRegionDest == sourceRegionDest
                    && x.Sort == sort)
                .OrderByDescending(x => x.ObservedAtUtc)
                .Select(x => x.Marketplace)
                .FirstOrDefaultAsync(cancellationToken);

            return ContextResolution.Success(new MarketContext(
                marketplace ?? "wildberries",
                sourceCategory,
                sourceSubcategory,
                rankQuery,
                sourceRegionDest,
                sort));
        }

        if (latestRankRunId is not null)
        {
            var contexts = await _dbContext.ParserRankSnapshotRows
                .AsNoTracking()
                .Where(x => x.ParserRunId == latestRankRunId)
                .Where(x => sourceCategory == null || x.SourceCategory == sourceCategory)
                .Where(x => sourceSubcategory == null || x.SourceSubcategory == sourceSubcategory)
                .Where(x => rankQuery == null || x.Query == rankQuery)
                .Where(x => sourceRegionDest == null || x.SourceRegionDest == sourceRegionDest)
                .Where(x => x.Sort == sort)
                .Select(x => new MarketContext(
                    x.Marketplace,
                    x.SourceCategory,
                    x.SourceSubcategory,
                    x.Query,
                    x.SourceRegionDest,
                    x.Sort))
                .Distinct()
                .Take(2)
                .ToListAsync(cancellationToken);

            return contexts.Count == 1
                ? ContextResolution.Success(contexts[0])
                : ContextResolution.Failure("Укажите категорию, подкатегорию, запрос и регион для анализа.");
        }

        return ContextResolution.Failure("Укажите категорию, подкатегорию, запрос и регион для анализа.");
    }

    private async Task<string?> ResolveLatestRankRunIdAsync(
        MarketContext context,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.Marketplace == context.Marketplace
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && (x.Sort ?? DefaultSort) == context.Sort)
            .GroupBy(x => x.ParserRunId)
            .Select(x => new
            {
                ParserRunId = x.Key,
                ObservedAtUtc = x.Max(row => row.ObservedAtUtc)
            })
            .OrderByDescending(x => x.ObservedAtUtc)
            .Select(x => x.ParserRunId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> ResolveBaselineRankRunIdAsync(
        string latestRankRunId,
        MarketContext context,
        int topN,
        Coverage latestCoverage,
        CancellationToken cancellationToken)
    {
        var latestObservedAtUtc = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x => x.ParserRunId == latestRankRunId)
            .MaxAsync(x => x.ObservedAtUtc, cancellationToken);

        var candidates = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId != latestRankRunId
                && x.ObservedAtUtc < latestObservedAtUtc
                && x.Marketplace == context.Marketplace
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && (x.Sort ?? DefaultSort) == context.Sort)
            .GroupBy(x => x.ParserRunId)
            .Select(x => new
            {
                ParserRunId = x.Key,
                ObservedAtUtc = x.Max(row => row.ObservedAtUtc)
            })
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var rows = await LoadRankRowsAsync(candidate.ParserRunId, context, topN, cancellationToken);
            var coverage = await LoadCoverageAsync(candidate.ParserRunId, context, topN, rows, cancellationToken);
            if (coverage.IsFull && IsComparableFingerprint(latestCoverage.RequestFingerprint, coverage.RequestFingerprint))
                return candidate.ParserRunId;
        }

        return null;
    }

    private async Task<string?> ResolveClosestProductRunIdAsync(
        MarketContext context,
        DateTime targetAtUtc,
        CancellationToken cancellationToken)
    {
        var successfulProductRuns = _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.Kind == ProductKind && x.ManifestStatus == SucceededStatus)
            .Select(x => x.ParserRunId);

        var candidates = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x =>
                successfulProductRuns.Contains(x.ParserRunId)
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.SourceRegionDest == context.SourceRegionDest)
            .GroupBy(x => x.ParserRunId)
            .Select(x => new ProductRunCandidate(
                x.Key,
                x.Max(row => row.ParsedAtUtc)))
            .ToListAsync(cancellationToken);

        return candidates
            .OrderBy(x => Math.Abs((x.ObservedAtUtc - targetAtUtc).Ticks))
            .Select(x => x.ParserRunId)
            .FirstOrDefault();
    }

    private async Task<List<RankRow>> LoadRankRowsAsync(
        string parserRunId,
        MarketContext context,
        int topN,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId == parserRunId
                && x.AbsolutePosition <= topN
                && x.Marketplace == context.Marketplace
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && (x.Sort ?? DefaultSort) == context.Sort)
            .OrderBy(x => x.AbsolutePosition)
            .ThenBy(x => x.Page)
            .ThenBy(x => x.PositionOnPage)
            .Select(x => new RankRow(
                x.ParserRunId,
                x.Marketplace,
                x.SourceCategory,
                x.SourceSubcategory,
                x.Query,
                x.SourceRegionDest,
                x.Sort,
                x.RequestFingerprint,
                x.Page,
                x.AbsolutePosition,
                x.WbProductId,
                x.WbRootId,
                x.ObservedAtUtc))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .Select(x => x.OrderBy(row => row.AbsolutePosition).First())
            .OrderBy(x => x.AbsolutePosition)
            .Take(topN)
            .ToList();
    }

    private async Task<Coverage> LoadCoverageAsync(
        string parserRunId,
        MarketContext context,
        int topN,
        IReadOnlyList<RankRow> rankRows,
        CancellationToken cancellationToken)
    {
        if (rankRows.Count < topN)
            return Coverage.Invalid("partial", SingleFingerprintOrNull(rankRows.Select(x => x.RequestFingerprint)));

        var pages = rankRows
            .Select(x => x.Page)
            .Distinct()
            .ToList();
        var fetches = await _dbContext.ParserRankPageFetches
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId == parserRunId
                && pages.Contains(x.Page)
                && x.Marketplace == context.Marketplace
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && (x.Sort ?? DefaultSort) == context.Sort)
            .Select(x => new
            {
                x.Page,
                x.Status,
                x.ProductCount,
                x.RequestFingerprint
            })
            .ToListAsync(cancellationToken);

        var successfulPages = fetches
            .Where(x => IsSuccessfulFetch(x.Status) && x.ProductCount > 0)
            .Select(x => x.Page)
            .ToHashSet();
        var isFull = pages.All(successfulPages.Contains);
        var fingerprint = SingleFingerprintOrNull(rankRows.Select(x => x.RequestFingerprint));

        return isFull
            ? Coverage.Full(fingerprint)
            : Coverage.Invalid("partial", fingerprint);
    }

    private async Task<IReadOnlyDictionary<string, ProductRow>> LoadProductsAsync(
        string? parserRunId,
        MarketContext context,
        IEnumerable<string> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, ProductRow>(StringComparer.Ordinal);

        var successfulProductRuns = _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.Kind == ProductKind && x.ManifestStatus == SucceededStatus)
            .Select(x => x.ParserRunId);

        var rawRows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x =>
                successfulProductRuns.Contains(x.ParserRunId)
                && ids.Contains(x.WbProductId)
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory)
            .Select(x => new
            {
                x.Id,
                x.ParserRunId,
                x.WbProductId,
                x.WbRootId,
                x.Name,
                x.BrandName,
                x.SellerName,
                x.PriceRegular,
                x.PriceDiscounted,
                x.PriceWbWallet,
                x.TotalQuantity,
                x.ReviewRating,
                x.FeedbackCount,
                x.ImageUrls,
                x.ImageCount,
                x.ParsedAtUtc
            })
            .ToListAsync(cancellationToken);

        var rows = rawRows
            .Select(x => new ProductRow(
                x.Id,
                x.ParserRunId,
                x.WbProductId,
                x.WbRootId,
                x.Name,
                x.BrandName,
                x.SellerName,
                FirstImageUrl(x.ImageUrls),
                x.PriceRegular,
                x.PriceDiscounted,
                x.PriceWbWallet,
                x.TotalQuantity,
                x.ReviewRating,
                x.FeedbackCount,
                x.ImageCount,
                x.ParsedAtUtc))
            .ToList();

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(row => parserRunId != null && row.ParserRunId == parserRunId)
                    .ThenByDescending(row => row.ParsedAtUtc)
                    .First(),
                StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<ProductRow>> LoadLatestProductsForMapAsync(
        MarketContext context,
        CancellationToken cancellationToken)
    {
        var rawRows = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x =>
                x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.SourceRegionDest == context.SourceRegionDest)
            .Select(x => new
            {
                x.Id,
                x.ParserRunId,
                x.WbProductId,
                x.WbRootId,
                x.Name,
                x.BrandName,
                x.SellerName,
                x.PriceRegular,
                x.PriceDiscounted,
                x.PriceWbWallet,
                x.TotalQuantity,
                x.ReviewRating,
                x.FeedbackCount,
                x.ImageUrls,
                x.ImageCount,
                x.ParsedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rawRows
            .Select(x => new ProductRow(
                x.Id,
                x.ParserRunId,
                x.WbProductId,
                x.WbRootId,
                x.Name,
                x.BrandName,
                x.SellerName,
                FirstImageUrl(x.ImageUrls),
                x.PriceRegular,
                x.PriceDiscounted,
                x.PriceWbWallet,
                x.TotalQuantity,
                x.ReviewRating,
                x.FeedbackCount,
                x.ImageCount,
                x.ParsedAtUtc))
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .Select(x => x
                .OrderByDescending(row => row.ParsedAtUtc)
                .ThenByDescending(row => row.Id)
                .First())
            .OrderByDescending(x => x.ParsedAtUtc)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, ProductDetailSignal>> LoadProductDetailsAsync(
        IEnumerable<string> productIds,
        string? productRunId,
        CancellationToken cancellationToken)
    {
        var ids = productIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, ProductDetailSignal>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x => ids.Contains(x.WbProductId) && x.Status == SucceededStatus)
            .OrderByDescending(x => productRunId != null && x.InputProductsParserRunId == productRunId)
            .ThenByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var row = x.First();
                    return new ProductDetailSignal(
                        row.Description,
                        MarketIntelligenceBucketEvaluator.CountCharacteristics(UsableJsonElement(row.Characteristics) ?? UsableJsonElement(row.GroupedOptions)),
                        row.MediaCount);
                },
                StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadDeliveryBucketsAsync(
        MarketContext context,
        IEnumerable<string> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var rows = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.WbProductId)
                && x.Marketplace == context.Marketplace
                && x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.SourceRegionDest == context.SourceRegionDest)
            .Select(x => new
            {
                x.WbProductId,
                x.VisibleDeliveryDate,
                ObservedAtUtc = x.VisibleDeliveryObservedAtUtc ?? x.ObservedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var row = x.OrderByDescending(value => value.ObservedAtUtc).First();
                    return MarketIntelligenceBucketEvaluator.EvaluateDelivery(row.VisibleDeliveryDate, row.ObservedAtUtc);
                },
                StringComparer.Ordinal);
    }

    private static PriceQualityMapDto BuildPriceQualityMap(
        IReadOnlyList<ProductRow> products,
        IReadOnlyDictionary<string, ProductDetailSignal> details,
        IReadOnlyDictionary<string, string> deliveryBuckets,
        IReadOnlyDictionary<string, int> rankPositions)
    {
        var points = products
            .Select(product =>
            {
                details.TryGetValue(product.WbProductId, out var detail);
                deliveryBuckets.TryGetValue(product.WbProductId, out var deliveryBucket);
                rankPositions.TryGetValue(product.WbProductId, out var position);
                var imageCount = detail?.MediaCount ?? product.ImageCount;
                var quality = MarketIntelligenceBucketEvaluator.EvaluateQuality(
                    product.ReviewRating,
                    product.FeedbackCount,
                    imageCount,
                    detail?.Description,
                    detail?.CharacteristicsCount,
                    hasProduct: true,
                    detail is not null);

                return new PriceQualityPointDto(
                    product.WbProductId,
                    product.WbRootId,
                    product.Id.ToString(),
                    product.Name,
                    product.ThumbnailUrl,
                    CurrentPrice(product),
                    product.ReviewRating,
                    product.FeedbackCount,
                    product.TotalQuantity,
                    position == 0 ? (int?)null : position,
                    product.SellerName,
                    product.BrandName,
                    quality.Bucket,
                    quality.Reasons,
                    deliveryBucket ?? "unknown");
            })
            .ToList();

        var summary = BuildPriceQualitySummary(points);
        var limitations = new List<string>();
        if (points.Any(x => x.ProductRowId is null))
            limitations.Add("Для части точек нет данных карточек, они показаны без перехода в карточку.");
        if (points.Any(x => x.QualityBucket == "unknown"))
            limitations.Add("Для части товаров недостаточно данных для оценки качества.");

        return new PriceQualityMapDto(points, summary, Deduplicate(limitations));
    }

    private static PriceQualityMapSummaryDto BuildPriceQualitySummary(IReadOnlyList<PriceQualityPointDto> points)
    {
        var strong = points.Count(x => x.QualityBucket == "strong");
        var medium = points.Count(x => x.QualityBucket == "medium");
        var weak = points.Count(x => x.QualityBucket == "weak");
        var unknown = points.Count(x => x.QualityBucket == "unknown");
        var withoutRating = points.Count(x => !x.Rating.HasValue || x.Rating.Value <= 0);
        var medianPrice = Median(points.Select(x => x.Price));
        var medianRating = Median(points.Select(x => x.Rating).Where(x => x.HasValue && x.Value > 0));

        return new PriceQualityMapSummaryDto(
            points.Count,
            withoutRating,
            strong,
            medium,
            weak,
            unknown,
            medianPrice,
            medianRating,
            BuildPriceQualityInsight(points, medianPrice));
    }

    private static string BuildPriceQualityInsight(IReadOnlyList<PriceQualityPointDto> points, decimal? medianPrice)
    {
        if (points.Count == 0)
            return "Недостаточно данных для карты цены и качества.";

        var strongExpensive = points.Count(x =>
            x.QualityBucket == "strong"
            && x.Price.HasValue
            && medianPrice.HasValue
            && x.Price.Value > medianPrice.Value);
        var weakCheap = points.Count(x =>
            x.QualityBucket == "weak"
            && x.Price.HasValue
            && medianPrice.HasValue
            && x.Price.Value <= medianPrice.Value);
        var withoutRating = points.Count(x => !x.Rating.HasValue || x.Rating.Value <= 0);

        var parts = new List<string>();
        if (strongExpensive > 0)
            parts.Add($"дорогих сильных товаров: {strongExpensive}");
        if (weakCheap > 0)
            parts.Add($"дешевых слабых товаров: {weakCheap}");
        if (withoutRating > 0)
            parts.Add($"без рейтинга: {withoutRating}");

        return parts.Count == 0
            ? "Карта показывает распределение видимых товаров по цене и рейтингу."
            : $"На карте выделены {string.Join(", ", parts)}.";
    }

    private static IReadOnlyList<MarketEventDto> BuildEvents(
        IReadOnlyList<RankedProduct> latestItems,
        IReadOnlyList<RankedProduct> baselineItems,
        IReadOnlyDictionary<string, ProductRow> baselineProducts,
        IReadOnlyDictionary<string, ProductRow> latestProducts,
        DateTime observedAtUtc)
    {
        var latestByProduct = latestItems.ToDictionary(x => x.Rank.WbProductId, StringComparer.Ordinal);
        var baselineByProduct = baselineItems.ToDictionary(x => x.Rank.WbProductId, StringComparer.Ordinal);
        var threshold = Math.Max(10, Math.Min(20, (int)Math.Ceiling(latestItems.Count * 0.20m)));
        var events = new List<EventCandidate>();

        foreach (var item in latestItems.Where(x => !baselineByProduct.ContainsKey(x.Rank.WbProductId)))
        {
            events.Add(new EventCandidate(
                new MarketEventDto(
                    "entered_checked_range",
                    "visibility",
                    "medium",
                    "Карточка вошла в проверенный диапазон",
                    $"Товар появился в проверенном диапазоне на позиции {item.Rank.AbsolutePosition}.",
                    item.Rank.WbProductId,
                    item.Product?.WbRootId ?? item.Rank.WbRootId,
                    item.Product?.Name,
                    item.Product?.BrandName,
                    item.Product?.SellerName,
                    item.Product?.Id.ToString(),
                    item.Product?.ThumbnailUrl,
                    null,
                    item.Rank.AbsolutePosition.ToString(),
                    observedAtUtc,
                    0.9m,
                    MetadataLimitations(item.Product)),
                SeverityRank: 1,
                SortPosition: item.Rank.AbsolutePosition,
                SortDelta: 0));
        }

        foreach (var item in baselineItems.Where(x => !latestByProduct.ContainsKey(x.Rank.WbProductId)))
        {
            events.Add(new EventCandidate(
                new MarketEventDto(
                    "left_checked_range",
                    "visibility",
                    "medium",
                    "Карточка вышла из проверенного диапазона",
                    $"Товар был на позиции {item.Rank.AbsolutePosition}, но не найден в текущем проверенном диапазоне.",
                    item.Rank.WbProductId,
                    item.Product?.WbRootId ?? item.Rank.WbRootId,
                    item.Product?.Name,
                    item.Product?.BrandName,
                    item.Product?.SellerName,
                    item.Product?.Id.ToString(),
                    item.Product?.ThumbnailUrl,
                    item.Rank.AbsolutePosition.ToString(),
                    null,
                    observedAtUtc,
                    0.9m,
                    MetadataLimitations(item.Product)),
                SeverityRank: 1,
                SortPosition: item.Rank.AbsolutePosition,
                SortDelta: 0));
        }

        foreach (var item in latestItems)
        {
            if (!baselineByProduct.TryGetValue(item.Rank.WbProductId, out var baseline))
                continue;

            var delta = baseline.Rank.AbsolutePosition - item.Rank.AbsolutePosition;
            if (Math.Abs(delta) >= threshold)
            {
                events.Add(new EventCandidate(
                    new MarketEventDto(
                        "sharp_position_move",
                        "visibility",
                        Math.Abs(delta) >= 50 ? "high" : "medium",
                        "Карточка заметно изменила позицию",
                        $"Позиция изменилась в проверенном диапазоне: было {baseline.Rank.AbsolutePosition}, стало {item.Rank.AbsolutePosition}.",
                        item.Rank.WbProductId,
                        item.Product?.WbRootId ?? item.Rank.WbRootId,
                        item.Product?.Name,
                        item.Product?.BrandName,
                        item.Product?.SellerName,
                        item.Product?.Id.ToString(),
                        item.Product?.ThumbnailUrl,
                        baseline.Rank.AbsolutePosition.ToString(),
                        item.Rank.AbsolutePosition.ToString(),
                        observedAtUtc,
                        0.85m,
                        MetadataLimitations(item.Product)),
                    SeverityRank: Math.Abs(delta) >= 50 ? 0 : 1,
                    SortPosition: item.Rank.AbsolutePosition,
                    SortDelta: Math.Abs(delta)));
            }

            if (!latestProducts.TryGetValue(item.Rank.WbProductId, out var latestProduct)
                || !baselineProducts.TryGetValue(item.Rank.WbProductId, out var baselineProduct))
            {
                continue;
            }

            var latestPrice = CurrentPrice(latestProduct);
            var baselinePrice = CurrentPrice(baselineProduct);
            if (latestPrice.HasValue && baselinePrice.HasValue && latestPrice.Value != baselinePrice.Value)
            {
                events.Add(new EventCandidate(
                    new MarketEventDto(
                        "visible_price_change",
                        "price",
                        "low",
                        "Цена изменилась в публичном наблюдении",
                        $"Текущая видимая цена изменилась: было {FormatMoney(baselinePrice)}, стало {FormatMoney(latestPrice)}.",
                        item.Rank.WbProductId,
                        latestProduct.WbRootId ?? item.Rank.WbRootId,
                        latestProduct.Name,
                        latestProduct.BrandName,
                        latestProduct.SellerName,
                        latestProduct.Id.ToString(),
                        latestProduct.ThumbnailUrl,
                        FormatMoney(baselinePrice),
                        FormatMoney(latestPrice),
                        observedAtUtc,
                        0.8m,
                        []),
                    SeverityRank: 2,
                    SortPosition: item.Rank.AbsolutePosition,
                    SortDelta: 0));
            }

            var latestDiscount = HasVisibleDiscount(latestProduct);
            var baselineDiscount = HasVisibleDiscount(baselineProduct);
            if (latestDiscount != baselineDiscount)
            {
                events.Add(new EventCandidate(
                    new MarketEventDto(
                        "visible_discount_change",
                        "promo_price",
                        "low",
                        "Изменились видимые промо-признаки",
                        latestDiscount
                            ? "У товара появилось видимое снижение цены относительно регулярной цены."
                            : "Видимое снижение цены относительно регулярной цены больше не найдено.",
                        item.Rank.WbProductId,
                        latestProduct.WbRootId ?? item.Rank.WbRootId,
                        latestProduct.Name,
                        latestProduct.BrandName,
                        latestProduct.SellerName,
                        latestProduct.Id.ToString(),
                        latestProduct.ThumbnailUrl,
                        baselineDiscount ? "есть" : "нет",
                        latestDiscount ? "есть" : "нет",
                        observedAtUtc,
                        0.75m,
                        []),
                    SeverityRank: 2,
                    SortPosition: item.Rank.AbsolutePosition,
                    SortDelta: 0));
            }
        }

        return events
            .OrderBy(x => x.SeverityRank)
            .ThenBy(x => x.SortPosition)
            .ThenByDescending(x => x.SortDelta)
            .Take(MaxEvents)
            .Select(x => x.Event)
            .ToList();
    }

    private static IReadOnlyList<CompetitorWeaknessDto> BuildWeaknesses(
        IReadOnlyList<RankedProduct> latestItems)
    {
        var feedbackMedian = Median(latestItems.Select(x => x.Product?.FeedbackCount));
        var ratingMedian = Median(latestItems.Select(x => x.Product?.ReviewRating));
        var priceMedian = Median(latestItems.Select(x => CurrentPrice(x.Product)));
        var stockMedian = Median(latestItems.Select(x => ExactStockQuantity(x.Product?.TotalQuantity)));
        var candidates = new List<WeaknessCandidate>();

        foreach (var item in latestItems)
        {
            var product = item.Product;
            if (product is null)
                continue;

            var lowFeedback = feedbackMedian.HasValue
                && product.FeedbackCount.HasValue
                && product.FeedbackCount.Value < feedbackMedian.Value;
            var lowRating = ratingMedian.HasValue
                && product.ReviewRating.HasValue
                && product.ReviewRating.Value <= ratingMedian.Value - RatingWeaknessDelta;

            if (lowFeedback && lowRating)
            {
                candidates.Add(Weakness(
                    item,
                    "weak_trust_strong_visibility",
                    "high",
                    0,
                    "Карточка заметна, но доверительные признаки слабее ниши",
                    "Карточка заметна в выдаче, но отзывы и рейтинг слабее ориентира по нише.",
                    $"{product.FeedbackCount} отзывов; рейтинг {FormatDecimal(product.ReviewRating)}",
                    $"медиана отзывов {FormatDecimal(feedbackMedian)}; медиана рейтинга {FormatDecimal(ratingMedian)}",
                    "Стоит изучить карточку и предложение конкурента."));
            }

            if (lowFeedback)
            {
                candidates.Add(Weakness(
                    item,
                    "high_position_low_reviews",
                    "medium",
                    1,
                    "Сильная позиция, слабая база отзывов",
                    "Карточка заметна в выдаче, но отзывов меньше, чем медиана в проверенном диапазоне.",
                    product.FeedbackCount?.ToString(),
                    FormatDecimal(feedbackMedian),
                    "Стоит изучить карточку и предложение конкурента."));
            }

            if (lowRating)
            {
                candidates.Add(Weakness(
                    item,
                    "high_position_low_rating",
                    "medium",
                    1,
                    "Высокая позиция при рейтинге ниже ниши",
                    "Карточка находится в проверенном диапазоне, но рейтинг ниже ориентира по нише.",
                    FormatDecimal(product.ReviewRating),
                    FormatDecimal(ratingMedian),
                    "Это слабый доверительный признак для ручного анализа."));
            }

            var stock = ToStockValue(product.TotalQuantity);
            if (stock.Status == "exact" && stock.Value is >= 1 and <= 5)
            {
                candidates.Add(Weakness(
                    item,
                    "high_position_low_stock",
                    "high",
                    0,
                    "Видимый конкурент с низким остатком",
                    "Карточка заметна в выдаче, но точный видимый остаток низкий.",
                    stock.DisplayValue,
                    FormatDecimal(stockMedian),
                    "Это зона для изучения доступности предложения."));
            }

            var price = CurrentPrice(product);
            if (priceMedian.HasValue && price.HasValue && price.Value >= priceMedian.Value * HighPriceMultiplier)
            {
                candidates.Add(Weakness(
                    item,
                    "high_price_vs_median",
                    "low",
                    2,
                    "Цена выше медианы ниши",
                    "Карточка заметна в выдаче при цене выше медианного ориентира.",
                    FormatMoney(price),
                    FormatMoney(priceMedian),
                    "Стоит сравнить наполнение карточки и условия предложения."));
            }
        }

        return candidates
            .OrderBy(x => x.Weakness.Position)
            .ThenBy(x => x.SeverityRank)
            .Take(MaxWeaknesses)
            .Select(x => x.Weakness)
            .ToList();
    }

    private static PublicMarketPromoPressureDto BuildPromoPressure(
        IReadOnlyList<RankedProduct> latestItems,
        IReadOnlyList<RankedProduct> baselineItems,
        bool isComparable)
    {
        var latestProducts = latestItems.Select(x => x.Product).Where(x => x is not null).Cast<ProductRow>().ToList();
        var baselineProducts = baselineItems.Select(x => x.Product).Where(x => x is not null).Cast<ProductRow>().ToList();
        var latestShare = Share(latestProducts, HasVisibleDiscount);
        var baselineShare = isComparable ? Share(baselineProducts, HasVisibleDiscount) : null;
        var walletShare = Share(latestProducts, product => product.PriceWbWallet.HasValue);

        return new PublicMarketPromoPressureDto(
            [
                new PressureSummaryDto(
                    "visible_discount_share",
                    "Доля товаров с видимым снижением цены",
                    "Показывает, какая часть проверенного диапазона имеет промо-признаки по публичным ценам.",
                    latestShare,
                    baselineShare,
                    Delta(latestShare, baselineShare),
                    "%",
                    []),
                new PressureSummaryDto(
                    "wallet_price_share",
                    "Доля товаров с WB-ценой",
                    "Показывает, у какой части товаров найдена отдельная цена кошелька.",
                    walletShare,
                    null,
                    null,
                    "%",
                    [])
            ],
            []);
    }

    private static PublicMarketPricePressureDto BuildPricePressure(
        IReadOnlyList<RankedProduct> latestItems,
        IReadOnlyList<RankedProduct> baselineItems,
        bool isComparable)
    {
        var latestCurrentMedian = Median(latestItems.Select(x => CurrentPrice(x.Product)));
        var latestRegularMedian = Median(latestItems.Select(x => x.Product?.PriceRegular));
        var baselineCurrentMedian = isComparable ? Median(baselineItems.Select(x => CurrentPrice(x.Product))) : null;
        int? leaderPriceDrops = isComparable ? CountLeaderPriceDrops(latestItems, baselineItems) : null;
        var highPriceVisibleProducts = latestItems
            .Where(x => x.Product is not null)
            .Where(x => latestCurrentMedian.HasValue && CurrentPrice(x.Product).HasValue && CurrentPrice(x.Product)!.Value > latestCurrentMedian.Value)
            .OrderBy(x => x.Rank.AbsolutePosition)
            .Take(20)
            .Select(x => new HighPriceVisibleProductDto(
                x.Rank.WbProductId,
                x.Product!.WbRootId ?? x.Rank.WbRootId,
                x.Product.Name,
                x.Product.BrandName,
                x.Product.SellerName,
                x.Product.Id.ToString(),
                x.Product.ThumbnailUrl,
                x.Rank.AbsolutePosition,
                CurrentPrice(x.Product),
                latestCurrentMedian,
                "Высокая цена сохраняет видимость в проверенном диапазоне.",
                []))
            .ToList();

        return new PublicMarketPricePressureDto(
            [
                new PressureSummaryDto(
                    "median_current_price",
                    "Медианная текущая цена",
                    "Ориентир по текущим видимым ценам в проверенном диапазоне.",
                    latestCurrentMedian,
                    baselineCurrentMedian,
                    Delta(latestCurrentMedian, baselineCurrentMedian),
                    "руб.",
                    []),
                new PressureSummaryDto(
                    "median_regular_price",
                    "Медианная регулярная цена",
                    "Ориентир по регулярным публичным ценам, где они доступны.",
                    latestRegularMedian,
                    null,
                    null,
                    "руб.",
                    []),
                new PressureSummaryDto(
                    "leader_price_drops",
                    "Снижения цены среди видимых товаров",
                    "Количество товаров, у которых текущая видимая цена ниже прошлого сопоставимого наблюдения.",
                    leaderPriceDrops,
                    null,
                    null,
                    "шт.",
                    isComparable ? [] : ["Для сравнения цен нужно сопоставимое предыдущее наблюдение."])
            ],
            highPriceVisibleProducts,
            []);
    }

    private static PublicMarketStockPressureDto BuildStockPressure(
        IReadOnlyList<RankedProduct> latestItems)
    {
        var stocks = latestItems
            .Select(x => new
            {
                Item = x,
                Stock = ToStockValue(x.Product?.TotalQuantity)
            })
            .ToList();
        var lowStockProducts = stocks
            .Where(x => x.Stock.Status == "exact" && x.Stock.Value is >= 1 and <= 5)
            .OrderBy(x => x.Item.Rank.AbsolutePosition)
            .Take(MaxHighRankLowStockProducts)
            .Select(x => new HighRankLowStockProductDto(
                x.Item.Rank.WbProductId,
                x.Item.Product?.WbRootId ?? x.Item.Rank.WbRootId,
                x.Item.Product?.Name,
                x.Item.Product?.BrandName,
                x.Item.Product?.SellerName,
                x.Item.Product?.Id.ToString(),
                x.Item.Product?.ThumbnailUrl,
                x.Item.Rank.AbsolutePosition,
                x.Stock,
                "Точный видимый остаток низкий, карточку стоит изучить отдельно.",
                MetadataLimitations(x.Item.Product)))
            .ToList();

        return new PublicMarketStockPressureDto(
            stocks.Count(x => x.Stock.Status == "exact" && x.Stock.Value == 0),
            stocks.Count(x => x.Stock.Status == "exact" && x.Stock.Value is >= 1 and <= 5),
            stocks.Count(x => x.Stock.Status == "capped"),
            stocks.Count(x => x.Stock.Status == "unknown"),
            BuildStockStatusBreakdown(stocks.Select(x => x.Stock)),
            lowStockProducts,
            []);
    }

    private static IReadOnlyList<StockStatusSummaryDto> BuildStockStatusBreakdown(
        IEnumerable<StockValueDto> stocks)
    {
        return stocks
            .GroupBy(x => x.Status)
            .OrderBy(x => x.Key switch
            {
                "exact" => 0,
                "capped" => 1,
                _ => 2
            })
            .Select(x => new StockStatusSummaryDto(x.Key, x.Count(), x.First()))
            .ToList();
    }

    private static ConcentrationSummaryDto BuildConcentration(
        IReadOnlyList<RankedProduct> latestItems)
    {
        var denominator = latestItems.Count == 0 ? 1 : latestItems.Count;
        var sellerLeaders = latestItems
            .Where(x => !string.IsNullOrWhiteSpace(x.Product?.SellerName))
            .GroupBy(x => x.Product!.SellerName!)
            .Where(x => x.Count() > 1)
            .Select(x => new ConcentrationLeaderDto(
                x.Key,
                x.Count(),
                Percent(x.Count(), denominator),
                "Один продавец занимает несколько мест в проверенном диапазоне."))
            .OrderByDescending(x => x.SlotsCount)
            .ThenByDescending(x => x.SharePercent)
            .ThenBy(x => x.Name)
            .Take(MaxLeaders)
            .ToList();
        var brandLeaders = latestItems
            .Where(x => !string.IsNullOrWhiteSpace(x.Product?.BrandName))
            .GroupBy(x => x.Product!.BrandName!)
            .Where(x => x.Count() > 1)
            .Select(x => new ConcentrationLeaderDto(
                x.Key,
                x.Count(),
                Percent(x.Count(), denominator),
                "Бренд занимает несколько мест в проверенном диапазоне."))
            .OrderByDescending(x => x.SlotsCount)
            .ThenByDescending(x => x.SharePercent)
            .ThenBy(x => x.Name)
            .Take(MaxLeaders)
            .ToList();
        var rootClusters = latestItems
            .Select(x => new
            {
                WbRootId = x.Product?.WbRootId ?? x.Rank.WbRootId,
                x.Rank.WbProductId,
                x.Rank.AbsolutePosition
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!)
            .Select(x => new
            {
                WbRootId = x.Key,
                ProductCount = x.Select(item => item.WbProductId).Distinct(StringComparer.Ordinal).Count(),
                BestPosition = x.Min(item => item.AbsolutePosition)
            })
            .Where(x => x.ProductCount > 1)
            .OrderByDescending(x => x.ProductCount)
            .ThenBy(x => x.BestPosition)
            .Take(MaxLeaders)
            .Select(x => new RootClusterDto(
                x.WbRootId,
                x.ProductCount,
                x.BestPosition,
                "В выдаче много вариантов одного товара."))
            .ToList();

        return new ConcentrationSummaryDto(sellerLeaders, brandLeaders, rootClusters, []);
    }

    private static WeaknessCandidate Weakness(
        RankedProduct item,
        string type,
        string severity,
        int severityRank,
        string title,
        string description,
        string? metricValue,
        string? referenceValue,
        string explanation)
    {
        return new WeaknessCandidate(
            new CompetitorWeaknessDto(
                type,
                severity,
                title,
                description,
                item.Rank.WbProductId,
                item.Product?.WbRootId ?? item.Rank.WbRootId,
                item.Product?.Name,
                item.Product?.BrandName,
                item.Product?.SellerName,
                item.Product?.Id.ToString(),
                item.Product?.ThumbnailUrl,
                item.Rank.AbsolutePosition,
                metricValue,
                referenceValue,
                explanation,
                []),
            severityRank);
    }

    private static bool MatchesContext(
        string marketplace,
        string? sourceCategory,
        string? sourceSubcategory,
        string query,
        string? sourceRegionDest,
        string? sort,
        MarketContext context)
    {
        return marketplace == context.Marketplace
            && sourceCategory == context.SourceCategory
            && sourceSubcategory == context.SourceSubcategory
            && query == context.Query
            && sourceRegionDest == context.SourceRegionDest
            && (sort ?? DefaultSort) == context.Sort;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsSuccessfulFetch(string status)
    {
        return string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComparableFingerprint(string? latest, string? baseline)
    {
        return string.IsNullOrWhiteSpace(latest)
            || string.IsNullOrWhiteSpace(baseline)
            || string.Equals(latest, baseline, StringComparison.Ordinal);
    }

    private static string? SingleFingerprintOrNull(IEnumerable<string> fingerprints)
    {
        var values = fingerprints
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        return values.Count == 1 ? values[0] : null;
    }

    private static StockValueDto ToStockValue(int? totalQuantity)
    {
        return totalQuantity switch
        {
            null => new StockValueDto("unknown", null, "неизвестно"),
            40 => new StockValueDto("capped", 40, "≥40"),
            _ => new StockValueDto("exact", totalQuantity.Value, totalQuantity.Value.ToString())
        };
    }

    private static int? ExactStockQuantity(int? totalQuantity)
    {
        var stock = ToStockValue(totalQuantity);
        return stock.Status == "exact" ? stock.Value : null;
    }

    private static decimal? CurrentPrice(ProductRow? product)
    {
        return product?.PriceDiscounted ?? product?.PriceWbWallet ?? product?.PriceRegular;
    }

    private static bool HasVisibleDiscount(ProductRow product)
    {
        return product.PriceRegular.HasValue
            && product.PriceDiscounted.HasValue
            && product.PriceRegular.Value > product.PriceDiscounted.Value;
    }

    private static decimal? Median(IEnumerable<int?> values)
    {
        return Median(values.Where(x => x.HasValue).Select(x => (decimal)x!.Value));
    }

    private static decimal? Median(IEnumerable<decimal?> values)
    {
        return Median(values.Where(x => x.HasValue).Select(x => x!.Value));
    }

    private static decimal? Median(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(x => x).ToList();
        if (ordered.Count == 0)
            return null;

        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2m;
    }

    private static decimal? Share(IReadOnlyList<ProductRow> products, Func<ProductRow, bool> predicate)
    {
        return products.Count == 0 ? null : Percent(products.Count(predicate), products.Count);
    }

    private static decimal Percent(int count, int total)
    {
        return total <= 0 ? 0 : Math.Round(count * 100m / total, 2);
    }

    private static decimal? Delta(decimal? current, decimal? baseline)
    {
        return current.HasValue && baseline.HasValue
            ? current.Value - baseline.Value
            : null;
    }

    private static JsonElement? UsableJsonElement(JsonDocument? document)
    {
        if (document is null)
            return null;

        return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? document.RootElement
            : null;
    }

    private static int CountLeaderPriceDrops(
        IReadOnlyList<RankedProduct> latestItems,
        IReadOnlyList<RankedProduct> baselineItems)
    {
        var baselineByProduct = baselineItems
            .Where(x => x.Product is not null)
            .ToDictionary(x => x.Rank.WbProductId, x => x.Product!, StringComparer.Ordinal);

        return latestItems.Count(item =>
            item.Product is not null
            && baselineByProduct.TryGetValue(item.Rank.WbProductId, out var baseline)
            && CurrentPrice(item.Product).HasValue
            && CurrentPrice(baseline).HasValue
            && CurrentPrice(item.Product)!.Value < CurrentPrice(baseline)!.Value);
    }

    private static string? FormatDecimal(decimal? value)
    {
        return value?.ToString("0.##");
    }

    private static string? FormatMoney(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.##") : null;
    }

    private static IReadOnlyList<string> MetadataLimitations(ProductRow? product)
    {
        return product is null ? ["Нет данных карточки для этого товара в выбранном топе."] : [];
    }

    private static string? FirstImageUrl(JsonDocument? imageUrls)
    {
        if (imageUrls?.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var image in imageUrls.RootElement.EnumerateArray())
        {
            if (image.ValueKind != JsonValueKind.String)
                continue;

            var value = image.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> values)
    {
        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private sealed record ContextResolution(MarketContext? Value, string? Error)
    {
        public bool IsSuccess => Error is null;

        public static ContextResolution Success(MarketContext value) => new(value, null);
        public static ContextResolution Failure(string error) => new(null, error);
    }

    private sealed record MarketContext(
        string Marketplace,
        string? SourceCategory,
        string? SourceSubcategory,
        string Query,
        string? SourceRegionDest,
        string? Sort);

    private sealed record RankRow(
        string ParserRunId,
        string Marketplace,
        string? SourceCategory,
        string? SourceSubcategory,
        string Query,
        string? SourceRegionDest,
        string? Sort,
        string RequestFingerprint,
        int Page,
        int AbsolutePosition,
        string WbProductId,
        string? WbRootId,
        DateTime ObservedAtUtc);

    private sealed record ProductRow(
        Guid Id,
        string ParserRunId,
        string WbProductId,
        string? WbRootId,
        string Name,
        string? BrandName,
        string? SellerName,
        string? ThumbnailUrl,
        decimal? PriceRegular,
        decimal? PriceDiscounted,
        decimal? PriceWbWallet,
        int? TotalQuantity,
        decimal? ReviewRating,
        int? FeedbackCount,
        int? ImageCount,
        DateTime ParsedAtUtc);

    private sealed record RankedProduct(RankRow Rank, ProductRow? Product);

    private sealed record ProductDetailSignal(string? Description, int CharacteristicsCount, int? MediaCount);

    private sealed record Coverage(bool IsFull, string Status, string? RequestFingerprint)
    {
        public static Coverage Full(string? requestFingerprint) => new(true, "complete", requestFingerprint);
        public static Coverage Invalid(string status, string? requestFingerprint) => new(false, status, requestFingerprint);
    }

    private sealed record ProductRunCandidate(string ParserRunId, DateTime ObservedAtUtc);

    private sealed record EventCandidate(MarketEventDto Event, int SeverityRank, int SortPosition, int SortDelta);

    private sealed record WeaknessCandidate(CompetitorWeaknessDto Weakness, int SeverityRank);
}
