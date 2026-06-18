using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using AshmesMarketplaces.Application.MarketRecommendations.Options;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public sealed class MarketHotProductsSnapshotBuilder : IMarketHotProductsSnapshotBuilder
{
    private const string ProductsKind = "products";
    private const string RanksKind = "ranks";
    private const string SucceededStatus = "succeeded";
    private const string MarketplaceWildberries = "wildberries";
    private const string PositionStateObserved = "observed";
    private const string PositionStateBeyondObservedRange = "beyondObservedRange";
    private const string PositionStateUnknown = "unknown";
    private const int DefaultBoundedProducts = 1000;
    private const int MaxProductsPerRun = 100_000;
    private const int RecentReviewWindowSize = 10;
    private const int ReviewSentimentVersion = 2;
    private const string ReviewScopeProduct = "product";
    private const string ReviewScopeRoot = "root";
    private const long WbWarehouseDtypeFlag = 8;
    private static readonly TimeSpan RecentReviewLookback = TimeSpan.FromDays(14);

    private readonly ApplicationDbContext _dbContext;

    public MarketHotProductsSnapshotBuilder(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<MarketHotProductsSnapshot>> BuildAsync(
        RecalculateHotProductsRequest request,
        IntelligenceOptions options,
        CancellationToken cancellationToken)
    {
        var maxProducts = request.MaxProducts ?? DefaultBoundedProducts;
        if (maxProducts > MaxProductsPerRun)
        {
            return ServiceResult<MarketHotProductsSnapshot>.BadRequest(
                $"Requested maxProducts exceeds supported run maximum of {MaxProductsPerRun}.");
        }

        var explicitProductRunId = !string.IsNullOrWhiteSpace(request.ProductParserRunId);
        var productRunId = explicitProductRunId ? request.ProductParserRunId!.Trim() : null;
        var rankRunId = string.IsNullOrWhiteSpace(request.RankParserRunId)
            ? null
            : request.RankParserRunId.Trim();

        var productRowsQuery = _dbContext.ParserProductRows
            .AsNoTracking();

        if (explicitProductRunId)
        {
            productRowsQuery = productRowsQuery.Where(x => x.ParserRunId == productRunId);
        }
        else
        {
            var productRunIds = await ResolveParserRunIdsAsync(ProductsKind, cancellationToken);
            if (productRunIds.Count == 0)
                return ServiceResult<MarketHotProductsSnapshot>.NotFound("No successful parser product run was found.");

            productRowsQuery = productRowsQuery.Where(x => productRunIds.Contains(x.ParserRunId));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceCategory))
        {
            var category = request.SourceCategory.Trim().ToLowerInvariant();
            productRowsQuery = productRowsQuery.Where(x =>
                x.SourceCategory != null && x.SourceCategory.Trim().ToLower() == category);
        }

        if (!string.IsNullOrWhiteSpace(request.SourceSubcategory))
        {
            var subcategory = request.SourceSubcategory.Trim().ToLowerInvariant();
            productRowsQuery = productRowsQuery.Where(x =>
                x.SourceSubcategory != null && x.SourceSubcategory.Trim().ToLower() == subcategory);
        }

        var products = explicitProductRunId
            ? await productRowsQuery
                .OrderBy(x => x.SourceLineNumber)
                .ThenBy(x => x.Id)
                .Take(maxProducts)
                .ToListAsync(cancellationToken)
            : await LoadLatestProductRowsAsync(productRowsQuery, maxProducts, cancellationToken);

        if (products.Count == 0)
        {
            return ServiceResult<MarketHotProductsSnapshot>.NotFound(
                "No parser product rows were found for the selected Market Analytics scope.");
        }

        var positions = await LoadPositionsAsync(products, rankRunId, cancellationToken);
        var reviews = await LoadReviewEvidenceAsync(products, DateTime.UtcNow, cancellationToken);
        var productDetails = await LoadProductDetailsAsync(products, productRunId, cancellationToken);
        var deliveryProfiles = await LoadDeliveryProfilesAsync(products, cancellationToken);
        var reviewRunIds = reviews.Values
            .Select(x => x.LatestReviewRunId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var sourceSubcategories = products
            .Select(x => x.SourceSubcategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var distinctCategories = products
            .Select(x => x.SourceCategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();
        var sourceCategory = string.IsNullOrWhiteSpace(request.SourceCategory)
            ? distinctCategories.Count == 1 ? distinctCategories[0] : null
            : request.SourceCategory.Trim();

        var snapshots = products
            .Select(product =>
            {
                positions.TryGetValue(product.Id, out var position);
                reviews.TryGetValue(product.Id, out var review);
                productDetails.TryGetValue(product.Id, out var detail);
                deliveryProfiles.TryGetValue(product.Id, out var deliveryProfile);
                review ??= ReviewEvidence.Empty;
                position ??= ProductPosition.Unknown(product.SourceCategory, product.SourceSubcategory);

                var productKey = $"wildberries:{product.WbProductId}";
                var rating = product.ReviewRating ?? product.RatingRounded;
                var feature = new MarketProductFeatureDto(
                    productKey,
                    product.WbProductId,
                    product.WbRootId,
                    product.Name,
                    product.BrandName,
                    product.SellerName,
                    product.SourceCategory,
                    product.SourceSubcategory,
                    product.PriceDiscounted,
                    product.PriceRegular,
                    product.PriceWbWallet,
                    rating,
                    product.FeedbackCount,
                    review.ParsedReviewCount,
                    review.ParsedReplyCount,
                    position.Position,
                    position.PositionState,
                    position.ObservedRangeLimit,
                    product.TotalQuantity,
                    product.ParsedAtUtc,
                    Description: detail?.Description,
                    Characteristics: detail?.Characteristics,
                    ImageCount: detail?.MediaCount ?? product.ImageCount,
                    ReviewSignals: new MarketProductReviewSignalDto(
                        review.ParsedReviewCount,
                        review.ParsedReplyCount,
                        review.RatedReviewCount,
                        review.AverageRating,
                        review.LowRatingReviewCount,
                        review.NegativeTextReviewCount,
                        review.BadReviewCount,
                        review.ReviewWindowSize,
                        review.RecentTwoWeeksCount,
                        review.LatestReviewRunId,
                        review.SentimentVersion,
                        review.ReviewScope,
                        review.NegativeReviewEvidence),
                    DeliveryProfile: deliveryProfile);

                return new MarketProductFeatureSnapshot(
                    product.Id,
                    feature,
                    product.Name,
                    product.BrandName,
                    product.SellerName,
                    product.PriceDiscounted,
                    product.PriceRegular,
                    product.PriceWbWallet,
                    rating,
                    product.FeedbackCount,
                    review.ParsedReviewCount,
                    review.ParsedReplyCount,
                    position.Position,
                    position.PositionState,
                    position.ObservedRangeLimit,
                    product.TotalQuantity);
            })
            .ToList();

        return ServiceResult<MarketHotProductsSnapshot>.Success(
            new MarketHotProductsSnapshot(
                MarketplaceWildberries,
                sourceCategory,
                sourceSubcategories,
                productRunId,
                rankRunId,
                reviewRunIds,
                snapshots));
    }

    private async Task<string?> ResolveLatestParserRunIdAsync(string kind, CancellationToken cancellationToken)
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

    private async Task<IReadOnlyList<string>> ResolveParserRunIdsAsync(string kind, CancellationToken cancellationToken)
    {
        return await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x => x.Kind == kind && x.ManifestStatus == SucceededStatus)
            .OrderBy(x => x.StartedAtUtc)
            .ThenBy(x => x.ParserRunId)
            .Select(x => x.ParserRunId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ParserProductRow>> LoadLatestProductRowsAsync(
        IQueryable<ParserProductRow> productRowsQuery,
        int maxProducts,
        CancellationToken cancellationToken)
    {
        var latestRowIds = productRowsQuery
            .GroupBy(x => x.WbProductId)
            .Select(group => group
                .OrderByDescending(x => x.ParsedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .First());

        return await productRowsQuery
            .Where(x => latestRowIds.Contains(x.Id))
            .OrderBy(x => x.SourceCategory)
            .ThenBy(x => x.SourceSubcategory)
            .ThenBy(x => x.SourceLineNumber)
            .ThenBy(x => x.Id)
            .Take(maxProducts)
            .ToListAsync(cancellationToken);
    }

    public static IReadOnlyList<HotProductsProductSelectionRow> SelectLatestProductRowsForDefaultScope(
        IReadOnlyList<HotProductsProductSelectionRow> rows,
        int maxProducts)
    {
        var selectedByProductId = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.WbProductId))
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(row => row.ParsedAtUtc)
                    .ThenByDescending(row => row.Id)
                    .First(),
                StringComparer.Ordinal);

        return rows
            .Where(x => selectedByProductId.TryGetValue(x.WbProductId, out var selected) && selected.Id == x.Id)
            .Take(Math.Max(maxProducts, 0))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, ProductPosition>> LoadPositionsAsync(
        IReadOnlyList<ParserProductRow> products,
        string? rankRunId,
        CancellationToken cancellationToken)
    {
        var productIds = products.Select(x => x.WbProductId).Distinct(StringComparer.Ordinal).ToList();
        var rootIds = products
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var categories = products
            .Select(x => x.SourceCategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var subcategories = products
            .Select(x => x.SourceSubcategory)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var rows = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x => (rankRunId == null || x.ParserRunId == rankRunId)
                && (productIds.Contains(x.WbProductId)
                    || (x.WbRootId != null && rootIds.Contains(x.WbRootId))
                    || ((x.SourceCategory == null || categories.Contains(x.SourceCategory))
                        && x.SourceSubcategory != null
                        && subcategories.Contains(x.SourceSubcategory))))
            .Select(x => new RankRow(
                x.WbProductId,
                x.WbRootId,
                x.SourceCategory,
                x.SourceSubcategory,
                x.SourceRegionDest,
                x.Query,
                x.AbsolutePosition,
                x.Page,
                x.PositionOnPage,
                x.ObservedAtUtc))
            .ToListAsync(cancellationToken);

        var exactBest = rows
            .Where(x => productIds.Contains(x.WbProductId))
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => BestRank(x), StringComparer.Ordinal);
        var rootBest = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId) && rootIds.Contains(x.WbRootId!))
            .GroupBy(x => x.WbRootId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => BestRank(x), StringComparer.Ordinal);
        var rootProductCounts = products
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var duplicateRootIds = rootProductCounts
            .Where(x => x.Value > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);
        var rankedRootIds = rows
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var exactRankedProductIds = exactBest.Keys.ToHashSet(StringComparer.Ordinal);
        var coverage = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceSubcategory))
            .GroupBy(x => new CoverageKey(x.SourceCategory, x.SourceSubcategory, x.SourceRegionDest))
            .ToDictionary(
                x => x.Key,
                x => new CoverageSummary(
                    x.Max(row => row.AbsolutePosition),
                    x.Select(row => row.Query).Distinct(StringComparer.Ordinal).Take(2).ToList(),
                    x.Max(row => row.ObservedAtUtc)));

        var result = new Dictionary<Guid, ProductPosition>();
        foreach (var product in products)
        {
            if (exactBest.TryGetValue(product.WbProductId, out var exact))
            {
                result[product.Id] = ProductPosition.Observed(exact.AbsolutePosition);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(product.WbRootId)
                && rootProductCounts.GetValueOrDefault(product.WbRootId) == 1
                && rootBest.TryGetValue(product.WbRootId, out var root))
            {
                result[product.Id] = ProductPosition.Observed(root.AbsolutePosition);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(product.WbRootId)
                && duplicateRootIds.Contains(product.WbRootId)
                && rankedRootIds.Contains(product.WbRootId)
                && !exactRankedProductIds.Contains(product.WbProductId))
            {
                result[product.Id] = ProductPosition.Unknown(product.SourceCategory, product.SourceSubcategory);
                continue;
            }

            var coverageKey = new CoverageKey(product.SourceCategory, product.SourceSubcategory, product.SourceRegionDest);
            if (!string.IsNullOrWhiteSpace(product.SourceSubcategory)
                && coverage.TryGetValue(coverageKey, out var coverageSummary))
            {
                result[product.Id] = ProductPosition.BeyondObservedRange(coverageSummary.ObservedRangeLimit);
                continue;
            }

            result[product.Id] = ProductPosition.Unknown(product.SourceCategory, product.SourceSubcategory);
        }

        return result;
    }

    private static IReadOnlyDictionary<Guid, ProductPosition> BuildUnknownPositions(IReadOnlyList<ParserProductRow> products)
    {
        return products.ToDictionary(
            x => x.Id,
            x => ProductPosition.Unknown(x.SourceCategory, x.SourceSubcategory));
    }

    private async Task<IReadOnlyDictionary<Guid, ProductDetailEvidence>> LoadProductDetailsAsync(
        IReadOnlyList<ParserProductRow> products,
        string? productRunId,
        CancellationToken cancellationToken)
    {
        var productIds = products
            .Select(x => x.WbProductId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var detailRows = await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId) && x.Status == SucceededStatus)
            .OrderByDescending(x => productRunId != null && x.InputProductsParserRunId == productRunId)
            .ThenByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .ToListAsync(cancellationToken);

        var detailsByProductId = detailRows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var result = new Dictionary<Guid, ProductDetailEvidence>();
        foreach (var product in products)
        {
            if (!detailsByProductId.TryGetValue(product.WbProductId, out var detail))
                continue;

            result[product.Id] = new ProductDetailEvidence(
                detail.Description,
                CloneUsableJson(detail.Characteristics) ?? CloneUsableJson(detail.GroupedOptions),
                detail.MediaCount);
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, MarketProductDeliveryProfileDto>> LoadDeliveryProfilesAsync(
        IReadOnlyList<ParserProductRow> products,
        CancellationToken cancellationToken)
    {
        var productIds = products
            .Select(x => x.WbProductId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (productIds.Count == 0)
            return new Dictionary<Guid, MarketProductDeliveryProfileDto>();

        var rows = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId)
                && x.DeliveryProfileKey != null
                && x.SourceRegionDest != null)
            .Select(x => new DeliveryProfileRow(
                x.WbProductId,
                x.SourceRegionDest,
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationAddress,
                x.TotalQuantityObserved,
                x.ProductTime1Raw,
                x.ProductTime2Raw,
                x.ProductDtypeRaw,
                x.VisibleDeliveryLabel,
                x.VisibleDeliveryDate,
                x.VisibleDeliveryStatus,
                x.ObservedAtUtc,
                x.SourceLineNumber))
            .ToListAsync(cancellationToken);

        var latestByProduct = rows
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .GroupBy(row => row.SourceRegionDest, StringComparer.Ordinal)
                    .Select(group => group
                        .OrderByDescending(row => row.ObservedAtUtc)
                        .ThenByDescending(row => row.SourceLineNumber)
                        .First())
                    .OrderBy(row => row.DeliveryProfileKey, StringComparer.Ordinal)
                    .ThenBy(row => row.SourceRegionDest, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        var result = new Dictionary<Guid, MarketProductDeliveryProfileDto>();
        foreach (var product in products)
        {
            if (!latestByProduct.TryGetValue(product.WbProductId, out var profileRows) || profileRows.Count == 0)
                continue;

            var destinations = profileRows
                .Select(row =>
                {
                    var calculated = WbVisibleDeliveryCalculator.Calculate(
                        row.ProductTime1Raw,
                        row.ProductTime2Raw,
                        row.ProductDtypeRaw,
                        row.TotalQuantityObserved,
                        row.ObservedAtUtc);

                    return new MarketProductDeliveryDestinationDto(
                        RegionKeyFrom(row),
                        RegionNameFrom(row),
                        row.DeliveryDestinationCity ?? row.DeliveryDestinationName,
                        row.DeliveryDestinationAddress,
                        calculated.Label ?? row.VisibleDeliveryLabel,
                        calculated.Date ?? row.VisibleDeliveryDate,
                        DeliveryHoursFrom(row),
                        DeliverySourceTypeFrom(row.ProductDtypeRaw),
                        row.TotalQuantityObserved,
                        calculated.ObservedAtUtc ?? row.ObservedAtUtc);
                })
                .Where(x => x.DeliveryHours.HasValue || x.VisibleDeliveryDate.HasValue)
                .ToList();

            if (destinations.Count > 0)
                result[product.Id] = new MarketProductDeliveryProfileDto(destinations);
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, ReviewEvidence>> LoadReviewEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        DateTime referenceUtc,
        CancellationToken cancellationToken)
    {
        var rootIds = products
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var productIds = products
            .Select(x => x.WbProductId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var reviewRows = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .Where(x => rootIds.Contains(x.SourceWbRootId) || productIds.Contains(x.WbProductId))
            .Select(x => new ReviewSignalRow(
                x.SourceWbRootId,
                x.WbProductId,
                x.Rating,
                x.Text,
                x.Pros,
                x.Cons,
                x.ParserRunId,
                x.ParsedAtUtc,
                x.SourceLineNumber,
                x.CreatedAtOnMp,
                x.ReviewIdOnMp))
            .ToListAsync(cancellationToken);

        var reviewByRoot = rootIds.Count == 0
            ? new Dictionary<string, ReviewAggregate>(StringComparer.Ordinal)
            : reviewRows
                .Where(x => rootIds.Contains(x.SourceWbRootId))
                .GroupBy(x => x.SourceWbRootId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => BuildReviewAggregate(x.Key, x, referenceUtc), StringComparer.Ordinal);
        var replyByRoot = rootIds.Count == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await _dbContext.ParserReviewReplyRows
                .AsNoTracking()
                .Where(x => rootIds.Contains(x.SourceWbRootId))
                .GroupBy(x => x.SourceWbRootId)
                .Select(x => new { x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, StringComparer.Ordinal, cancellationToken);
        var reviewByProduct = reviewRows
            .Where(x => productIds.Contains(x.WbProductId))
            .GroupBy(x => x.WbProductId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => BuildReviewAggregate(x.Key, x, referenceUtc), StringComparer.Ordinal);
        var replyByProduct = await _dbContext.ParserReviewReplyRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId))
            .GroupBy(x => x.WbProductId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, StringComparer.Ordinal, cancellationToken);

        var result = new Dictionary<Guid, ReviewEvidence>();
        foreach (var product in products)
        {
            var rootKey = product.WbRootId;
            ReviewAggregate? reviewAggregate = null;
            int replyCount = 0;
            var reviewScope = ReviewScopeProduct;

            reviewByProduct.TryGetValue(product.WbProductId, out reviewAggregate);
            replyByProduct.TryGetValue(product.WbProductId, out replyCount);

            if (reviewAggregate is null && !string.IsNullOrWhiteSpace(rootKey))
            {
                reviewByRoot.TryGetValue(rootKey, out reviewAggregate);
                replyByRoot.TryGetValue(rootKey, out replyCount);
                reviewScope = ReviewScopeRoot;
            }

            result[product.Id] = reviewAggregate is null
                ? ReviewEvidence.Empty
                : new ReviewEvidence(
                    reviewAggregate.ParsedReviewCount,
                    replyCount,
                    reviewAggregate.RatedReviewCount,
                    reviewAggregate.AverageRating,
                    reviewAggregate.LowRatingReviewCount,
                    reviewAggregate.NegativeTextReviewCount,
                    reviewAggregate.BadReviewCount,
                    reviewAggregate.ReviewWindowSize,
                    reviewAggregate.RecentTwoWeeksCount,
                    reviewAggregate.LatestReviewRunId,
                    reviewAggregate.SentimentVersion,
                    reviewScope,
                    reviewAggregate.NegativeReviewEvidence);
        }

        return result;
    }

    private static ReviewAggregate BuildReviewAggregate(
        string key,
        IEnumerable<ReviewSignalRow> rows,
        DateTime referenceUtc)
    {
        var selectedRows = BuildRecentReviewWindow(rows, referenceUtc);
        var validRatings = selectedRows
            .Select(x => x.Rating)
            .Where(x => x is >= 1 and <= 5)
            .Select(x => x!.Value)
            .ToList();
        var latestRunId = selectedRows
            .OrderByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .Select(x => x.ParserRunId)
            .FirstOrDefault();
        var cutoffUtc = referenceUtc - RecentReviewLookback;
        var lowRatingRows = selectedRows
            .Where(x => x.Rating is >= 1 and <= 3)
            .OrderBy(x => x.Rating)
            .ThenByDescending(x => x.CreatedAtOnMp)
            .ToList();

        return new ReviewAggregate(
            key,
            selectedRows.Count,
            RatedReviewCount: validRatings.Count,
            AverageRating: validRatings.Count == 0
                ? null
                : decimal.Round((decimal)validRatings.Average(), 2, MidpointRounding.AwayFromZero),
            LowRatingReviewCount: lowRatingRows.Count,
            NegativeTextReviewCount: 0,
            BadReviewCount: lowRatingRows.Count,
            ReviewWindowSize: selectedRows.Count,
            RecentTwoWeeksCount: selectedRows.Count(x => ReviewObservedAtUtc(x) >= cutoffUtc),
            latestRunId,
            ReviewSentimentVersion,
            lowRatingRows
                .Take(3)
                .Select(x => new ReviewNegativeEvidenceDto(
                    x.ReviewIdOnMp,
                    x.WbProductId,
                    x.Rating,
                    x.CreatedAtOnMp,
                    MakeSnippet(x.Text, x.Pros, x.Cons),
                    ["low_rating"],
                    0.85m))
                .ToList());
    }

    private static List<ReviewSignalRow> BuildRecentReviewWindow(
        IEnumerable<ReviewSignalRow> rows,
        DateTime referenceUtc)
    {
        var cutoffUtc = referenceUtc - RecentReviewLookback;
        var sortedRows = rows
            .GroupBy(ReviewDeduplicationKey, StringComparer.Ordinal)
            .Select(x => x
                .OrderByDescending(ReviewObservedAtUtc)
                .ThenByDescending(y => y.ParsedAtUtc)
                .ThenByDescending(y => y.SourceLineNumber)
                .First())
            .OrderByDescending(ReviewObservedAtUtc)
            .ThenByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .ToList();

        var recentRows = sortedRows
            .Where(x => ReviewObservedAtUtc(x) >= cutoffUtc)
            .Take(RecentReviewWindowSize)
            .ToList();

        if (recentRows.Count >= RecentReviewWindowSize)
            return recentRows;

        var selectedKeys = recentRows
            .Select(ReviewDeduplicationKey)
            .ToHashSet(StringComparer.Ordinal);
        var olderRows = sortedRows
            .Where(x => !selectedKeys.Contains(ReviewDeduplicationKey(x)))
            .Take(RecentReviewWindowSize - recentRows.Count);
        recentRows.AddRange(olderRows);
        return recentRows;
    }

    private static DateTime ReviewObservedAtUtc(ReviewSignalRow row) => row.CreatedAtOnMp ?? row.ParsedAtUtc;

    private static string ReviewDeduplicationKey(ReviewSignalRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.ReviewIdOnMp))
            return $"id:{row.ReviewIdOnMp.Trim()}";

        return $"fallback:{row.WbProductId}:{row.CreatedAtOnMp?.Ticks}:{row.SourceLineNumber}";
    }

    private static string MakeSnippet(params string?[] values)
    {
        var text = string.Join(
                ' ',
                values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim()))
            .Trim();

        return text.Length <= 180 ? text : $"{text[..177]}...";
    }

    private static JsonElement? CloneUsableJson(JsonDocument? document)
    {
        if (document is null)
            return null;

        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Array when root.GetArrayLength() == 0 => null,
            JsonValueKind.Object when !root.EnumerateObject().Any() => null,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => root.Clone()
        };
    }

    private static RankRow BestRank(IEnumerable<RankRow> rows)
    {
        return rows
            .OrderBy(x => x.AbsolutePosition)
            .ThenBy(x => x.Page)
            .ThenBy(x => x.PositionOnPage)
            .First();
    }

    private sealed record RankRow(
        string WbProductId,
        string? WbRootId,
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest,
        string Query,
        int AbsolutePosition,
        int Page,
        int PositionOnPage,
        DateTime ObservedAtUtc);

    private sealed record CoverageKey(string? SourceCategory, string? SourceSubcategory, string? SourceRegionDest);

    private sealed record CoverageSummary(int ObservedRangeLimit, IReadOnlyList<string> Queries, DateTime ObservedAtUtc);

    private sealed record ProductDetailEvidence(string? Description, JsonElement? Characteristics, int? MediaCount);

    private static string RegionKeyFrom(DeliveryProfileRow row)
    {
        var value = (row.DeliveryDestinationName ?? row.DeliveryDestinationCity ?? row.SourceRegionDest).Trim().ToLowerInvariant();
        return value switch
        {
            var x when x.Contains("моск", StringComparison.OrdinalIgnoreCase) => "central",
            var x when x.Contains("санкт", StringComparison.OrdinalIgnoreCase) || x.Contains("петербург", StringComparison.OrdinalIgnoreCase) => "northwest",
            var x when x.Contains("казан", StringComparison.OrdinalIgnoreCase) => "volga",
            var x when x.Contains("екатеринбург", StringComparison.OrdinalIgnoreCase) => "ural",
            var x when x.Contains("новосибирск", StringComparison.OrdinalIgnoreCase) => "siberia",
            var x when x.Contains("краснодар", StringComparison.OrdinalIgnoreCase) => "south",
            var x when x.Contains("хабаровск", StringComparison.OrdinalIgnoreCase) || x.Contains("владивосток", StringComparison.OrdinalIgnoreCase) => "far_east",
            _ => row.SourceRegionDest
        };
    }

    private static string RegionNameFrom(DeliveryProfileRow row) =>
        RegionKeyFrom(row) switch
        {
            "central" => "Центральный регион",
            "northwest" => "Северо-Запад",
            "volga" => "Поволжье",
            "ural" => "Урал",
            "siberia" => "Сибирь",
            "south" => "Юг",
            "far_east" => "Дальний Восток",
            _ => row.DeliveryDestinationName ?? row.DeliveryDestinationCity ?? row.SourceRegionDest
        };

    private static int? DeliveryHoursFrom(DeliveryProfileRow row) =>
        row.ProductTime1Raw.HasValue && row.ProductTime2Raw.HasValue
            ? row.ProductTime1Raw.Value + row.ProductTime2Raw.Value
            : null;

    private static string DeliverySourceTypeFrom(long? dtype)
    {
        if (!dtype.HasValue)
            return "unknown";

        return (dtype.Value & WbWarehouseDtypeFlag) != 0
            ? "wb_warehouse"
            : "seller_warehouse";
    }

    private sealed record ProductPosition(string PositionState, int? Position, int? ObservedRangeLimit)
    {
        public static ProductPosition Observed(int position) => new(PositionStateObserved, position, null);

        public static ProductPosition BeyondObservedRange(int observedRangeLimit) =>
            new(PositionStateBeyondObservedRange, null, observedRangeLimit);

        public static ProductPosition Unknown(string? sourceCategory, string? sourceSubcategory) =>
            new(PositionStateUnknown, null, null);
    }

    private sealed record ReviewAggregate(
        string Key,
        int ParsedReviewCount,
        int RatedReviewCount,
        decimal? AverageRating,
        int LowRatingReviewCount,
        int NegativeTextReviewCount,
        int BadReviewCount,
        int ReviewWindowSize,
        int RecentTwoWeeksCount,
        string? LatestReviewRunId,
        int SentimentVersion,
        IReadOnlyList<ReviewNegativeEvidenceDto> NegativeReviewEvidence);

    private sealed record ReviewEvidence(
        int ParsedReviewCount,
        int ParsedReplyCount,
        int RatedReviewCount,
        decimal? AverageRating,
        int LowRatingReviewCount,
        int NegativeTextReviewCount,
        int BadReviewCount,
        int ReviewWindowSize,
        int RecentTwoWeeksCount,
        string? LatestReviewRunId,
        int SentimentVersion,
        string ReviewScope,
        IReadOnlyList<ReviewNegativeEvidenceDto> NegativeReviewEvidence)
    {
        public static readonly ReviewEvidence Empty = new(0, 0, 0, null, 0, 0, 0, 0, 0, null, ReviewSentimentVersion, ReviewScopeProduct, []);
    }

    private sealed record ReviewSignalRow(
        string SourceWbRootId,
        string WbProductId,
        int? Rating,
        string? Text,
        string? Pros,
        string? Cons,
        string ParserRunId,
        DateTime ParsedAtUtc,
        long SourceLineNumber,
        DateTime? CreatedAtOnMp,
        string? ReviewIdOnMp);

    private sealed record DeliveryProfileRow(
        string WbProductId,
        string SourceRegionDest,
        string? DeliveryProfileKey,
        string? DeliveryDestinationName,
        string? DeliveryDestinationCity,
        string? DeliveryDestinationAddress,
        int? TotalQuantityObserved,
        int? ProductTime1Raw,
        int? ProductTime2Raw,
        long? ProductDtypeRaw,
        string? VisibleDeliveryLabel,
        DateTime? VisibleDeliveryDate,
        string? VisibleDeliveryStatus,
        DateTime ObservedAtUtc,
        long SourceLineNumber);

}
