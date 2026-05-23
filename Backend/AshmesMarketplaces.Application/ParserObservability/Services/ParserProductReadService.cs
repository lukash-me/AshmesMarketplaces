using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed class ParserProductReadService : IParserProductReadService
{
    private const string ProductsKind = "products";
    private const string RanksKind = "ranks";
    private const string SucceededStatus = "succeeded";
    private const string DefaultAttributionMode = "root_payload";

    private static readonly ParserProductReviewEvidenceDto EmptyReviewEvidence = new(
        RootFetchCount: 0,
        ParsedReviewCount: 0,
        ParsedReplyCount: 0,
        LatestReviewRunId: null,
        AttributionMode: DefaultAttributionMode,
        IsRootScoped: true,
        IsFullHistoryUnknown: true,
        HasCappedRootPayload: false);

    private readonly ApplicationDbContext _dbContext;

    public ParserProductReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetListAsync(
        ParserProductListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = _dbContext.ParserProductRows.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ParserRunId))
        {
            rows = rows.Where(x => x.ParserRunId == query.ParserRunId.Trim());
        }
        else
        {
            var latestProductRunId = await ResolveLatestParserRunIdAsync(ProductsKind, cancellationToken);
            if (latestProductRunId is null)
            {
                return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
                    new PagedResponse<ParserProductListItemDto>([], page, pageSize, 0));
            }

            rows = rows.Where(x => x.ParserRunId == latestProductRunId);
        }

        rows = ApplyFilters(rows, query);
        rows = ApplySort(rows, query.Sort);

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var evidence = await LoadEvidenceAsync(pageRows, cancellationToken);
        var items = pageRows
            .Select(row => MapToListItem(row, evidence))
            .ToList();

        return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
            new PagedResponse<ParserProductListItemDto>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ParserProductDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ParserProductDetailDto>.BadRequest("Parser product row id is required.");

        var row = await _dbContext.ParserProductRows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (row is null)
            return ServiceResult<ParserProductDetailDto>.NotFound("Parser product row was not found.");

        var sourceFile = await _dbContext.ParserFiles
            .AsNoTracking()
            .FirstAsync(x => x.Id == row.IdParserFile, cancellationToken);
        var evidence = await LoadEvidenceAsync([row], cancellationToken);

        return ServiceResult<ParserProductDetailDto>.Success(MapToDetail(row, sourceFile, evidence));
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

    private async Task<ProductEvidenceLookup> LoadEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
            return ProductEvidenceLookup.Empty;

        var ranks = await LoadRankSummariesAsync(products, cancellationToken);
        var reviews = await LoadReviewEvidenceAsync(products, cancellationToken);
        return new ProductEvidenceLookup(ranks, reviews);
    }

    private async Task<IReadOnlyDictionary<Guid, ParserProductRankSummaryDto>> LoadRankSummariesAsync(
        IReadOnlyList<ParserProductRow> products,
        CancellationToken cancellationToken)
    {
        var latestRankRunId = await ResolveLatestParserRunIdAsync(RanksKind, cancellationToken);
        if (latestRankRunId is null)
            return new Dictionary<Guid, ParserProductRankSummaryDto>();

        var rootIds = products
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct()
            .ToList();
        var fallbackProductIds = products
            .Where(x => string.IsNullOrWhiteSpace(x.WbRootId))
            .Select(x => x.WbProductId)
            .Distinct()
            .ToList();

        var candidates = new List<RankCandidate>();
        if (rootIds.Count > 0)
        {
            candidates.AddRange(await _dbContext.ParserRankSnapshotRows
                .AsNoTracking()
                .Where(x =>
                    x.ParserRunId == latestRankRunId
                    && x.WbRootId != null
                    && rootIds.Contains(x.WbRootId))
                .Select(x => new RankCandidate(
                    true,
                    x.WbRootId!,
                    x.AbsolutePosition,
                    x.Page,
                    x.PositionOnPage,
                    x.Query,
                    x.SourceCategory,
                    x.SourceSubcategory,
                    x.SourceRegionDest,
                    x.Sort,
                    x.ObservedAtUtc,
                    x.ParserRunId,
                    x.RankContextId))
                .ToListAsync(cancellationToken));
        }

        if (fallbackProductIds.Count > 0)
        {
            candidates.AddRange(await _dbContext.ParserRankSnapshotRows
                .AsNoTracking()
                .Where(x =>
                    x.ParserRunId == latestRankRunId
                    && fallbackProductIds.Contains(x.WbProductId))
                .Select(x => new RankCandidate(
                    false,
                    x.WbProductId,
                    x.AbsolutePosition,
                    x.Page,
                    x.PositionOnPage,
                    x.Query,
                    x.SourceCategory,
                    x.SourceSubcategory,
                    x.SourceRegionDest,
                    x.Sort,
                    x.ObservedAtUtc,
                    x.ParserRunId,
                    x.RankContextId))
                .ToListAsync(cancellationToken));
        }

        var summariesByMatchKey = candidates
            .GroupBy(x => new RankMatchKey(x.IsRootKey, x.Key))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var best = x
                        .OrderBy(candidate => candidate.AbsolutePosition)
                        .ThenBy(candidate => candidate.Page)
                        .ThenBy(candidate => candidate.PositionOnPage)
                        .First();
                    return new ParserProductRankSummaryDto(
                        best.AbsolutePosition,
                        best.Page,
                        best.PositionOnPage,
                        best.Query,
                        best.SourceCategory,
                        best.SourceSubcategory,
                        best.SourceRegionDest,
                        best.Sort,
                        best.ObservedAtUtc,
                        best.ParserRunId,
                        best.RankContextId,
                        x.Select(candidate => candidate.RankContextId).Distinct().Count());
                });

        var result = new Dictionary<Guid, ParserProductRankSummaryDto>();
        foreach (var product in products)
        {
            var matchKey = !string.IsNullOrWhiteSpace(product.WbRootId)
                ? new RankMatchKey(true, product.WbRootId)
                : new RankMatchKey(false, product.WbProductId);

            if (summariesByMatchKey.TryGetValue(matchKey, out var summary))
                result[product.Id] = summary;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, ParserProductReviewEvidenceDto>> LoadReviewEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        CancellationToken cancellationToken)
    {
        var rootIds = products
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct()
            .ToList();
        var fallbackProductIds = products
            .Where(x => string.IsNullOrWhiteSpace(x.WbRootId))
            .Select(x => x.WbProductId)
            .Distinct()
            .ToList();
        var productRunIds = products
            .Select(x => x.ParserRunId)
            .Distinct()
            .ToList();

        var sameRunBuckets = new Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket>();
        var rootScopedBuckets = new Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket>();

        await LoadReviewRowEvidenceAsync(
            rootIds,
            fallbackProductIds,
            productRunIds,
            requireProductRunLineage: true,
            sameRunBuckets,
            cancellationToken);
        await LoadReplyRowEvidenceAsync(
            rootIds,
            fallbackProductIds,
            productRunIds,
            requireProductRunLineage: true,
            sameRunBuckets,
            cancellationToken);

        await LoadRootScopedFetchEvidenceAsync(rootIds, rootScopedBuckets, cancellationToken);
        await LoadReviewRowEvidenceAsync(
            rootIds,
            fallbackProductIds,
            productRunIds: [],
            requireProductRunLineage: false,
            rootScopedBuckets,
            cancellationToken);
        await LoadReplyRowEvidenceAsync(
            rootIds,
            fallbackProductIds,
            productRunIds: [],
            requireProductRunLineage: false,
            rootScopedBuckets,
            cancellationToken);

        var result = new Dictionary<Guid, ParserProductReviewEvidenceDto>();
        foreach (var product in products)
        {
            var isRootKey = !string.IsNullOrWhiteSpace(product.WbRootId);
            var key = isRootKey ? product.WbRootId! : product.WbProductId;
            var rootScopedKey = new ReviewEvidenceKey(isRootKey, key, null);
            var sameRunKey = new ReviewEvidenceKey(isRootKey, key, product.ParserRunId);
            sameRunBuckets.TryGetValue(sameRunKey, out var sameRunBucket);
            rootScopedBuckets.TryGetValue(rootScopedKey, out var rootScopedBucket);

            var useSameRunBucket = sameRunBucket is not null
                && (sameRunBucket.ParsedReviewCount > 0 || sameRunBucket.ParsedReplyCount > 0);
            var selected = useSameRunBucket ? sameRunBucket! : rootScopedBucket;
            if (selected is null)
            {
                result[product.Id] = EmptyReviewEvidence;
                continue;
            }

            var rootFetchCount = useSameRunBucket && isRootKey && rootScopedBucket is not null
                ? rootScopedBucket.RootFetchCount
                : selected.RootFetchCount;
            var isFullHistoryUnknown = selected.IsFullHistoryUnknown
                || (useSameRunBucket && isRootKey && rootScopedBucket?.IsFullHistoryUnknown == true);
            var hasCappedRootPayload = selected.HasCappedRootPayload
                || (useSameRunBucket && isRootKey && rootScopedBucket?.HasCappedRootPayload == true);

            result[product.Id] = new ParserProductReviewEvidenceDto(
                rootFetchCount,
                selected.ParsedReviewCount,
                selected.ParsedReplyCount,
                selected.LatestReviewRunId,
                selected.AttributionMode ?? DefaultAttributionMode,
                IsRootScoped: true,
                isFullHistoryUnknown,
                hasCappedRootPayload);
        }

        return result;
    }

    private async Task LoadRootScopedFetchEvidenceAsync(
        IReadOnlyCollection<string> rootIds,
        Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket> buckets,
        CancellationToken cancellationToken)
    {
        if (rootIds.Count == 0)
            return;

        var rootFetches = await _dbContext.ParserReviewRootFetches
            .AsNoTracking()
            .Where(x => rootIds.Contains(x.SourceWbRootId))
            .GroupBy(x => x.SourceWbRootId)
            .Select(g => new RootFetchAggregate(
                g.Key,
                g.Count(),
                g.OrderByDescending(x => x.TimestampUtc)
                    .ThenByDescending(x => x.SourceLineNumber)
                    .Select(x => x.ParserRunId)
                    .FirstOrDefault(),
                g.Max(x => x.TimestampUtc),
                g.Any(x => x.IsFullHistoryUnknown),
                g.Any(x => x.IsCappedRootPayload)))
            .ToListAsync(cancellationToken);

        foreach (var row in rootFetches)
        {
            var bucket = GetBucket(buckets, new ReviewEvidenceKey(IsRootKey: true, row.Key, ProductRunId: null));
            bucket.RootFetchCount += row.RootFetchCount;
            bucket.IsFullHistoryUnknown |= row.IsFullHistoryUnknown;
            bucket.HasCappedRootPayload |= row.HasCappedRootPayload;
            bucket.SetLatest(row.LatestAtUtc, row.LatestRunId);
        }
    }

    private async Task LoadReviewRowEvidenceAsync(
        IReadOnlyCollection<string> rootIds,
        IReadOnlyCollection<string> fallbackProductIds,
        IReadOnlyCollection<string> productRunIds,
        bool requireProductRunLineage,
        Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket> buckets,
        CancellationToken cancellationToken)
    {
        var rows = _dbContext.ParserReviewRows.AsNoTracking();
        if (requireProductRunLineage)
        {
            rows = rows.Where(x =>
                x.InputProductsParserRunId != null
                && productRunIds.Contains(x.InputProductsParserRunId));
        }

        if (rootIds.Count > 0)
        {
            var rootRows = await rows
                .Where(x => rootIds.Contains(x.SourceWbRootId))
                .GroupBy(x => new
                {
                    Key = x.SourceWbRootId,
                    ProductRunId = requireProductRunLineage ? x.InputProductsParserRunId : null
                })
                .Select(g => new ReviewRowAggregate(
                    true,
                    g.Key.Key,
                    g.Key.ProductRunId,
                    g.Count(),
                    g.Where(x => x.IdReviewRootFetch.HasValue)
                        .Select(x => x.IdReviewRootFetch)
                        .Distinct()
                        .Count(),
                    g.OrderByDescending(x => x.ParsedAtUtc)
                        .ThenByDescending(x => x.SourceLineNumber)
                        .Select(x => x.ParserRunId)
                        .FirstOrDefault(),
                    g.Max(x => x.ParsedAtUtc),
                    g.Select(x => x.ReviewAttributionMode).FirstOrDefault(),
                    g.Any(x => x.IsFullHistoryUnknown),
                    g.Any(x => x.IsCappedRootPayload)))
                .ToListAsync(cancellationToken);

            AddReviewAggregates(rootRows, buckets);
        }

        if (fallbackProductIds.Count > 0)
        {
            var productRows = await rows
                .Where(x => fallbackProductIds.Contains(x.WbProductId))
                .GroupBy(x => new
                {
                    Key = x.WbProductId,
                    ProductRunId = requireProductRunLineage ? x.InputProductsParserRunId : null
                })
                .Select(g => new ReviewRowAggregate(
                    false,
                    g.Key.Key,
                    g.Key.ProductRunId,
                    g.Count(),
                    g.Where(x => x.IdReviewRootFetch.HasValue)
                        .Select(x => x.IdReviewRootFetch)
                        .Distinct()
                        .Count(),
                    g.OrderByDescending(x => x.ParsedAtUtc)
                        .ThenByDescending(x => x.SourceLineNumber)
                        .Select(x => x.ParserRunId)
                        .FirstOrDefault(),
                    g.Max(x => x.ParsedAtUtc),
                    g.Select(x => x.ReviewAttributionMode).FirstOrDefault(),
                    g.Any(x => x.IsFullHistoryUnknown),
                    g.Any(x => x.IsCappedRootPayload)))
                .ToListAsync(cancellationToken);

            AddReviewAggregates(productRows, buckets);
        }
    }

    private async Task LoadReplyRowEvidenceAsync(
        IReadOnlyCollection<string> rootIds,
        IReadOnlyCollection<string> fallbackProductIds,
        IReadOnlyCollection<string> productRunIds,
        bool requireProductRunLineage,
        Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket> buckets,
        CancellationToken cancellationToken)
    {
        var rows = _dbContext.ParserReviewReplyRows.AsNoTracking();
        if (requireProductRunLineage)
        {
            rows = rows.Where(x =>
                x.InputProductsParserRunId != null
                && productRunIds.Contains(x.InputProductsParserRunId));
        }

        if (rootIds.Count > 0)
        {
            var rootRows = await rows
                .Where(x => rootIds.Contains(x.SourceWbRootId))
                .GroupBy(x => new
                {
                    Key = x.SourceWbRootId,
                    ProductRunId = requireProductRunLineage ? x.InputProductsParserRunId : null
                })
                .Select(g => new ReplyRowAggregate(
                    true,
                    g.Key.Key,
                    g.Key.ProductRunId,
                    g.Count(),
                    g.Where(x => x.IdReviewRootFetch.HasValue)
                        .Select(x => x.IdReviewRootFetch)
                        .Distinct()
                        .Count(),
                    g.OrderByDescending(x => x.ParsedAtUtc)
                        .ThenByDescending(x => x.SourceLineNumber)
                        .Select(x => x.ParserRunId)
                        .FirstOrDefault(),
                    g.Max(x => x.ParsedAtUtc),
                    g.Select(x => x.ReviewAttributionMode).FirstOrDefault(),
                    g.Any(x => x.IsFullHistoryUnknown),
                    g.Any(x => x.IsCappedRootPayload)))
                .ToListAsync(cancellationToken);

            AddReplyAggregates(rootRows, buckets);
        }

        if (fallbackProductIds.Count > 0)
        {
            var productRows = await rows
                .Where(x => fallbackProductIds.Contains(x.WbProductId))
                .GroupBy(x => new
                {
                    Key = x.WbProductId,
                    ProductRunId = requireProductRunLineage ? x.InputProductsParserRunId : null
                })
                .Select(g => new ReplyRowAggregate(
                    false,
                    g.Key.Key,
                    g.Key.ProductRunId,
                    g.Count(),
                    g.Where(x => x.IdReviewRootFetch.HasValue)
                        .Select(x => x.IdReviewRootFetch)
                        .Distinct()
                        .Count(),
                    g.OrderByDescending(x => x.ParsedAtUtc)
                        .ThenByDescending(x => x.SourceLineNumber)
                        .Select(x => x.ParserRunId)
                        .FirstOrDefault(),
                    g.Max(x => x.ParsedAtUtc),
                    g.Select(x => x.ReviewAttributionMode).FirstOrDefault(),
                    g.Any(x => x.IsFullHistoryUnknown),
                    g.Any(x => x.IsCappedRootPayload)))
                .ToListAsync(cancellationToken);

            AddReplyAggregates(productRows, buckets);
        }
    }

    private static void AddReviewAggregates(
        IEnumerable<ReviewRowAggregate> rows,
        Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket> buckets)
    {
        foreach (var row in rows)
        {
            var bucket = GetBucket(buckets, new ReviewEvidenceKey(row.IsRootKey, row.Key, row.ProductRunId));
            bucket.ParsedReviewCount += row.ParsedReviewCount;
            if (!row.IsRootKey || row.ProductRunId is not null)
                bucket.RootFetchCount += row.RootFetchCount;
            bucket.AttributionMode ??= row.AttributionMode;
            bucket.IsFullHistoryUnknown |= row.IsFullHistoryUnknown;
            bucket.HasCappedRootPayload |= row.HasCappedRootPayload;
            bucket.SetLatest(row.LatestAtUtc, row.LatestRunId);
        }
    }

    private static void AddReplyAggregates(
        IEnumerable<ReplyRowAggregate> rows,
        Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket> buckets)
    {
        foreach (var row in rows)
        {
            var bucket = GetBucket(buckets, new ReviewEvidenceKey(row.IsRootKey, row.Key, row.ProductRunId));
            bucket.ParsedReplyCount += row.ParsedReplyCount;
            if (!row.IsRootKey || row.ProductRunId is not null)
                bucket.RootFetchCount += row.RootFetchCount;
            bucket.AttributionMode ??= row.AttributionMode;
            bucket.IsFullHistoryUnknown |= row.IsFullHistoryUnknown;
            bucket.HasCappedRootPayload |= row.HasCappedRootPayload;
            bucket.SetLatest(row.LatestAtUtc, row.LatestRunId);
        }
    }

    private static ReviewEvidenceBucket GetBucket(
        Dictionary<ReviewEvidenceKey, ReviewEvidenceBucket> buckets,
        ReviewEvidenceKey key)
    {
        if (buckets.TryGetValue(key, out var bucket))
            return bucket;

        bucket = new ReviewEvidenceBucket();
        buckets[key] = bucket;
        return bucket;
    }

    private static IQueryable<ParserProductRow> ApplyFilters(
        IQueryable<ParserProductRow> rows,
        ParserProductListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SourceCategory))
            rows = rows.Where(x => x.SourceCategory == query.SourceCategory.Trim());

        if (!string.IsNullOrWhiteSpace(query.SourceSubcategory))
            rows = rows.Where(x => x.SourceSubcategory == query.SourceSubcategory.Trim());

        if (!string.IsNullOrWhiteSpace(query.BrandName))
            rows = rows.Where(x => x.BrandName == query.BrandName.Trim());

        if (!string.IsNullOrWhiteSpace(query.SellerName))
            rows = rows.Where(x => x.SellerName == query.SellerName.Trim());

        if (!string.IsNullOrWhiteSpace(query.WbRootId))
            rows = rows.Where(x => x.WbRootId == query.WbRootId.Trim());

        if (query.PriceDiscountedFrom.HasValue)
            rows = rows.Where(x => x.PriceDiscounted >= query.PriceDiscountedFrom.Value);

        if (query.PriceDiscountedTo.HasValue)
            rows = rows.Where(x => x.PriceDiscounted <= query.PriceDiscountedTo.Value);

        if (query.ReviewRatingFrom.HasValue)
            rows = rows.Where(x => x.ReviewRating >= query.ReviewRatingFrom.Value);

        if (query.ReviewRatingTo.HasValue)
            rows = rows.Where(x => x.ReviewRating <= query.ReviewRatingTo.Value);

        if (query.FeedbackCountFrom.HasValue)
            rows = rows.Where(x => x.FeedbackCount >= query.FeedbackCountFrom.Value);

        if (query.FeedbackCountTo.HasValue)
            rows = rows.Where(x => x.FeedbackCount <= query.FeedbackCountTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.WbProductId, $"%{search}%")
                || (x.BrandName != null && EF.Functions.ILike(x.BrandName, $"%{search}%"))
                || (x.SellerName != null && EF.Functions.ILike(x.SellerName, $"%{search}%")));
        }

        return rows;
    }

    private static IOrderedQueryable<ParserProductRow> ApplySort(
        IQueryable<ParserProductRow> rows,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "name" => rows.OrderBy(x => x.Name).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-name" => rows.OrderByDescending(x => x.Name).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "parsedAtUtc" => rows.OrderBy(x => x.ParsedAtUtc).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "wbProductId" => rows.OrderBy(x => x.WbProductId).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-wbProductId" => rows.OrderByDescending(x => x.WbProductId).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "priceDiscounted" => rows.OrderBy(x => x.PriceDiscounted).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-priceDiscounted" => rows.OrderByDescending(x => x.PriceDiscounted).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "reviewRating" => rows.OrderBy(x => x.ReviewRating).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-reviewRating" => rows.OrderByDescending(x => x.ReviewRating).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            "feedbackCount" => rows.OrderBy(x => x.FeedbackCount).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-feedbackCount" => rows.OrderByDescending(x => x.FeedbackCount).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
            _ => rows.OrderByDescending(x => x.ParsedAtUtc).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id)
        };
    }

    private static ParserProductListItemDto MapToListItem(
        ParserProductRow row,
        ProductEvidenceLookup evidence)
    {
        return new ParserProductListItemDto(
            row.Id,
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
            row.DiscountPercent,
            row.RatingRounded,
            row.ReviewRating,
            row.FeedbackCount,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            GetImageUrls(row.ImageUrls).FirstOrDefault(),
            evidence.GetRank(row.Id),
            evidence.GetReviewEvidence(row.Id));
    }

    private static ParserProductDetailDto MapToDetail(
        ParserProductRow row,
        ParserFile sourceFile,
        ProductEvidenceLookup evidence)
    {
        return new ParserProductDetailDto(
            row.Id,
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
            row.DiscountPercent,
            row.RatingRounded,
            row.ReviewRating,
            row.FeedbackCount,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            row.Marketplace,
            row.SkuProduct,
            row.Entity,
            row.BrandIdOnMp,
            row.SellerIdOnMp,
            row.TotalQuantity,
            row.FeedbackCountSource,
            GetImageUrls(row.ImageUrls),
            row.ImageCount,
            row.SubjectParentId,
            row.SubjectId,
            row.SourceRegionDest,
            sourceFile.Kind,
            sourceFile.Sha256,
            row.SourceLineNumber,
            row.RowHash,
            evidence.GetRank(row.Id),
            evidence.GetReviewEvidence(row.Id));
    }

    private static IReadOnlyList<string> GetImageUrls(JsonDocument? imageUrls)
    {
        if (imageUrls is null || imageUrls.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return imageUrls.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    private sealed record ProductEvidenceLookup(
        IReadOnlyDictionary<Guid, ParserProductRankSummaryDto> Ranks,
        IReadOnlyDictionary<Guid, ParserProductReviewEvidenceDto> Reviews)
    {
        public static ProductEvidenceLookup Empty { get; } = new(
            new Dictionary<Guid, ParserProductRankSummaryDto>(),
            new Dictionary<Guid, ParserProductReviewEvidenceDto>());

        public ParserProductRankSummaryDto? GetRank(Guid productId)
        {
            return Ranks.TryGetValue(productId, out var rank) ? rank : null;
        }

        public ParserProductReviewEvidenceDto GetReviewEvidence(Guid productId)
        {
            return Reviews.TryGetValue(productId, out var evidence) ? evidence : EmptyReviewEvidence;
        }
    }

    private sealed record RankCandidate(
        bool IsRootKey,
        string Key,
        int AbsolutePosition,
        int Page,
        int PositionOnPage,
        string Query,
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest,
        string? Sort,
        DateTime ObservedAtUtc,
        string ParserRunId,
        string RankContextId);

    private readonly record struct RankMatchKey(bool IsRootKey, string Key);

    private sealed record RootFetchAggregate(
        string Key,
        int RootFetchCount,
        string? LatestRunId,
        DateTime LatestAtUtc,
        bool IsFullHistoryUnknown,
        bool HasCappedRootPayload);

    private sealed record ReviewRowAggregate(
        bool IsRootKey,
        string Key,
        string? ProductRunId,
        int ParsedReviewCount,
        int RootFetchCount,
        string? LatestRunId,
        DateTime LatestAtUtc,
        string? AttributionMode,
        bool IsFullHistoryUnknown,
        bool HasCappedRootPayload);

    private sealed record ReplyRowAggregate(
        bool IsRootKey,
        string Key,
        string? ProductRunId,
        int ParsedReplyCount,
        int RootFetchCount,
        string? LatestRunId,
        DateTime LatestAtUtc,
        string? AttributionMode,
        bool IsFullHistoryUnknown,
        bool HasCappedRootPayload);

    private readonly record struct ReviewEvidenceKey(bool IsRootKey, string Key, string? ProductRunId);

    private sealed class ReviewEvidenceBucket
    {
        public int RootFetchCount { get; set; }
        public int ParsedReviewCount { get; set; }
        public int ParsedReplyCount { get; set; }
        public string? LatestReviewRunId { get; private set; }
        public DateTime? LatestAtUtc { get; private set; }
        public string? AttributionMode { get; set; }
        public bool IsFullHistoryUnknown { get; set; }
        public bool HasCappedRootPayload { get; set; }

        public void SetLatest(DateTime? latestAtUtc, string? latestRunId)
        {
            if (!latestAtUtc.HasValue || string.IsNullOrWhiteSpace(latestRunId))
                return;

            if (!LatestAtUtc.HasValue || latestAtUtc.Value > LatestAtUtc.Value)
            {
                LatestAtUtc = latestAtUtc.Value;
                LatestReviewRunId = latestRunId;
            }
        }
    }
}
