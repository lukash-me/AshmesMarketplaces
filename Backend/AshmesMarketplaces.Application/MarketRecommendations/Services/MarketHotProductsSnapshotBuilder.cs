using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using AshmesMarketplaces.Application.MarketRecommendations.Options;
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
    private const int DefaultBoundedProducts = 100;

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
        var maxConfiguredProducts = Math.Max(options.MaxProductsPerRequest, 1);
        var maxProducts = request.MaxProducts ?? Math.Min(DefaultBoundedProducts, maxConfiguredProducts);
        if (maxProducts > maxConfiguredProducts)
        {
            return ServiceResult<MarketHotProductsSnapshot>.BadRequest(
                $"Requested maxProducts exceeds configured Intelligence maximum of {maxConfiguredProducts}.");
        }

        var productRunId = string.IsNullOrWhiteSpace(request.ProductParserRunId)
            ? await ResolveLatestParserRunIdAsync(ProductsKind, cancellationToken)
            : request.ProductParserRunId.Trim();
        if (string.IsNullOrWhiteSpace(productRunId))
            return ServiceResult<MarketHotProductsSnapshot>.NotFound("No successful parser product run was found.");

        var rankRunId = string.IsNullOrWhiteSpace(request.RankParserRunId)
            ? await ResolveLatestParserRunIdAsync(RanksKind, cancellationToken)
            : request.RankParserRunId.Trim();

        var productRowsQuery = _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.ParserRunId == productRunId);

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

        var products = await productRowsQuery
            .OrderBy(x => x.SourceLineNumber)
            .ThenBy(x => x.Id)
            .Take(maxProducts)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return ServiceResult<MarketHotProductsSnapshot>.NotFound(
                "No parser product rows were found for the selected Market Analytics scope.");
        }

        var positions = string.IsNullOrWhiteSpace(rankRunId)
            ? BuildUnknownPositions(products)
            : await LoadPositionsAsync(products, rankRunId, cancellationToken);
        var reviews = await LoadReviewEvidenceAsync(products, cancellationToken);
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
                    product.ParsedAtUtc);

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

    private async Task<IReadOnlyDictionary<Guid, ProductPosition>> LoadPositionsAsync(
        IReadOnlyList<ParserProductRow> products,
        string rankRunId,
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
            .Where(x => x.ParserRunId == rankRunId
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

    private async Task<IReadOnlyDictionary<Guid, ReviewEvidence>> LoadReviewEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
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

        var reviewByRoot = rootIds.Count == 0
            ? new Dictionary<string, ReviewAggregate>(StringComparer.Ordinal)
            : await _dbContext.ParserReviewRows
                .AsNoTracking()
                .Where(x => rootIds.Contains(x.SourceWbRootId))
                .GroupBy(x => x.SourceWbRootId)
                .Select(x => new ReviewAggregate(
                    x.Key,
                    x.Count(),
                    0,
                    x.OrderByDescending(row => row.ParsedAtUtc)
                        .ThenByDescending(row => row.SourceLineNumber)
                        .Select(row => row.ParserRunId)
                        .FirstOrDefault()))
                .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, cancellationToken);
        var replyByRoot = rootIds.Count == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await _dbContext.ParserReviewReplyRows
                .AsNoTracking()
                .Where(x => rootIds.Contains(x.SourceWbRootId))
                .GroupBy(x => x.SourceWbRootId)
                .Select(x => new { x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, StringComparer.Ordinal, cancellationToken);
        var reviewByProduct = await _dbContext.ParserReviewRows
            .AsNoTracking()
            .Where(x => productIds.Contains(x.WbProductId))
            .GroupBy(x => x.WbProductId)
            .Select(x => new ReviewAggregate(
                x.Key,
                x.Count(),
                0,
                x.OrderByDescending(row => row.ParsedAtUtc)
                    .ThenByDescending(row => row.SourceLineNumber)
                    .Select(row => row.ParserRunId)
                    .FirstOrDefault()))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, cancellationToken);
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
            if (!string.IsNullOrWhiteSpace(rootKey))
            {
                reviewByRoot.TryGetValue(rootKey, out reviewAggregate);
                replyByRoot.TryGetValue(rootKey, out replyCount);
            }

            if (reviewAggregate is null)
            {
                reviewByProduct.TryGetValue(product.WbProductId, out reviewAggregate);
                replyByProduct.TryGetValue(product.WbProductId, out replyCount);
            }

            result[product.Id] = reviewAggregate is null
                ? ReviewEvidence.Empty
                : new ReviewEvidence(reviewAggregate.ParsedReviewCount, replyCount, reviewAggregate.LatestReviewRunId);
        }

        return result;
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
        int ParsedReplyCount,
        string? LatestReviewRunId);

    private sealed record ReviewEvidence(
        int ParsedReviewCount,
        int ParsedReplyCount,
        string? LatestReviewRunId)
    {
        public static readonly ReviewEvidence Empty = new(0, 0, null);
    }
}
