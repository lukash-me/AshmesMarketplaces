using System.Globalization;
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
    private const string LogisticsKind = "logistics";
    private const string SucceededStatus = "succeeded";
    private const string DefaultAttributionMode = "root_payload";
    private const string PositionStateObserved = "observed";
    private const string PositionStateBeyondObservedRange = "beyondObservedRange";
    private const string PositionStateUnknown = "unknown";
    private const string WarningQuantityExactnessNotProven = "quantity_exactness_not_proven";
    private const string WarningWarehouseIdsAreExternalMarketplaceIds = "warehouse_ids_are_external_marketplace_ids";
    private const string WarningLatestProductsAndLogisticsOverlapIsPartial = "latest_products_and_logistics_overlap_is_partial";
    private const string WarningLatestLogisticsRunNotFound = "latest_logistics_run_not_found";

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
        var rows = await BuildEffectiveProductRowsAsync(query.ParserRunId, cancellationToken);
        if (rows is null)
        {
            return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
                new PagedResponse<ParserProductListItemDto>([], page, pageSize, 0));
        }

        rows = ApplyFilters(rows, query);

        int totalCount;
        List<ParserProductRow> pageRows;
        if (IsPositionSort(query.Sort))
        {
            (totalCount, pageRows) = await LoadPositionSortedRowsAsync(
                rows,
                page,
                pageSize,
                query.Sort!.Trim().StartsWith("-", StringComparison.Ordinal),
                cancellationToken);
        }
        else
        {
            rows = ApplySort(rows, query.Sort);
            totalCount = await rows.CountAsync(cancellationToken);
            pageRows = await rows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        var evidence = await LoadEvidenceAsync(pageRows, includeLogisticsDetail: false, cancellationToken);
        var items = pageRows
            .Select(row => MapToListItem(row, evidence))
            .ToList();

        return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
            new PagedResponse<ParserProductListItemDto>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ParserProductFilterOptionsDto>> GetFilterOptionsAsync(
        ParserProductFilterOptionsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await BuildEffectiveProductRowsAsync(query.ParserRunId, cancellationToken);
        if (rows is null)
            return ServiceResult<ParserProductFilterOptionsDto>.Success(EmptyFilterOptions());

        var searchedRows = ApplySearch(rows, query.Search);
        var categoryRows = searchedRows;
        var subcategoryRows = ApplyFilterOptionValue(searchedRows, nameof(ParserProductRow.SourceCategory), query.SourceCategory);
        var brandRows = ApplyFilterOptionValue(subcategoryRows, nameof(ParserProductRow.SourceSubcategory), query.SourceSubcategory);
        var sellerRows = ApplyFilterOptionValue(brandRows, nameof(ParserProductRow.BrandName), query.BrandName);

        var categories = await LoadDistinctOptionValuesAsync(
            categoryRows.Select(x => x.SourceCategory),
            cancellationToken);
        var subcategories = await LoadDistinctOptionValuesAsync(
            subcategoryRows.Select(x => x.SourceSubcategory),
            cancellationToken);
        var brands = await LoadDistinctOptionValuesAsync(
            brandRows.Select(x => x.BrandName),
            cancellationToken);
        var sellers = await LoadDistinctOptionValuesAsync(
            sellerRows.Select(x => x.SellerName),
            cancellationToken);

        return ServiceResult<ParserProductFilterOptionsDto>.Success(
            new ParserProductFilterOptionsDto(categories, subcategories, brands, sellers));
    }

    public async Task<ServiceResult<ParserProductLogisticsSummaryAggregateDto>> GetLogisticsSummaryAsync(
        ParserProductLogisticsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var productRunId = await ResolveEffectiveProductRunIdAsync(query.ParserRunId, cancellationToken);
        var latestLogisticsRunId = await ResolveLatestParserRunIdAsync(LogisticsKind, cancellationToken);

        if (productRunId is null)
        {
            return ServiceResult<ParserProductLogisticsSummaryAggregateDto>.Success(
                BuildLogisticsAggregate(
                    productRunId: null,
                    logisticsRunId: latestLogisticsRunId,
                    query,
                    productsTotal: 0,
                    observations: []));
        }

        var rows = _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => x.ParserRunId == productRunId);
        rows = ApplyLogisticsSummaryFilters(rows, query);

        var products = await rows.ToListAsync(cancellationToken);
        if (products.Count == 0 || latestLogisticsRunId is null)
        {
            return ServiceResult<ParserProductLogisticsSummaryAggregateDto>.Success(
                BuildLogisticsAggregate(
                    productRunId,
                    latestLogisticsRunId,
                    query,
                    products.Count,
                    observations: []));
        }

        var observations = await LoadSelectedLogisticsObservationsAsync(
            products,
            latestLogisticsRunId,
            cancellationToken);

        return ServiceResult<ParserProductLogisticsSummaryAggregateDto>.Success(
            BuildLogisticsAggregate(
                productRunId,
                latestLogisticsRunId,
                query,
                products.Count,
                observations));
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
        var evidence = await LoadEvidenceAsync([row], includeLogisticsDetail: true, cancellationToken);
        var details = await LoadProductDetailAsync(row, cancellationToken);

        return ServiceResult<ParserProductDetailDto>.Success(MapToDetail(row, sourceFile, evidence, details));
    }

    private async Task<IQueryable<ParserProductRow>?> BuildEffectiveProductRowsAsync(
        string? parserRunId,
        CancellationToken cancellationToken)
    {
        var rows = _dbContext.ParserProductRows.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(parserRunId))
            return rows.Where(x => x.ParserRunId == parserRunId.Trim());

        var latestProductRunId = await ResolveEffectiveProductRunIdAsync(parserRunId, cancellationToken);
        return latestProductRunId is null
            ? null
            : rows.Where(x => x.ParserRunId == latestProductRunId);
    }

    private async Task<string?> ResolveEffectiveProductRunIdAsync(
        string? parserRunId,
        CancellationToken cancellationToken)
    {
        return !string.IsNullOrWhiteSpace(parserRunId)
            ? parserRunId.Trim()
            : await ResolveLatestParserRunIdAsync(ProductsKind, cancellationToken);
    }

    private static ParserProductFilterOptionsDto EmptyFilterOptions()
    {
        return new ParserProductFilterOptionsDto([], [], [], []);
    }

    private static IQueryable<ParserProductRow> ApplySearch(
        IQueryable<ParserProductRow> rows,
        string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return rows;

        var search = searchText.Trim();
        return rows.Where(x =>
            EF.Functions.ILike(x.Name, $"%{search}%")
            || EF.Functions.ILike(x.WbProductId, $"%{search}%")
            || (x.BrandName != null && EF.Functions.ILike(x.BrandName, $"%{search}%"))
            || (x.SellerName != null && EF.Functions.ILike(x.SellerName, $"%{search}%")));
    }

    private static IQueryable<ParserProductRow> ApplyFilterOptionValue(
        IQueryable<ParserProductRow> rows,
        string fieldName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return rows;

        var normalized = value.Trim().ToLowerInvariant();
        return fieldName switch
        {
            nameof(ParserProductRow.SourceCategory) => rows.Where(x =>
                x.SourceCategory != null && x.SourceCategory.Trim().ToLower() == normalized),
            nameof(ParserProductRow.SourceSubcategory) => rows.Where(x =>
                x.SourceSubcategory != null && x.SourceSubcategory.Trim().ToLower() == normalized),
            nameof(ParserProductRow.BrandName) => rows.Where(x =>
                x.BrandName != null && x.BrandName.Trim().ToLower() == normalized),
            nameof(ParserProductRow.SellerName) => rows.Where(x =>
                x.SellerName != null && x.SellerName.Trim().ToLower() == normalized),
            _ => rows
        };
    }

    private static async Task<IReadOnlyList<string>> LoadDistinctOptionValuesAsync(
        IQueryable<string?> values,
        CancellationToken cancellationToken)
    {
        var rawValues = await values
            .Where(x => x != null)
            .Select(x => x!)
            .ToListAsync(cancellationToken);

        return rawValues
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x, StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: true))
            .ToList();
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

    private async Task<ParserProductDetailRow?> LoadProductDetailAsync(
        ParserProductRow product,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == product.WbProductId
                && x.Status == "succeeded"
                && (x.InputProductsParserRunId == product.ParserRunId || x.InputProductsParserRunId == null))
            .OrderByDescending(x => x.InputProductsParserRunId == product.ParserRunId)
            .ThenByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ProductEvidenceLookup> LoadEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        bool includeLogisticsDetail,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
            return ProductEvidenceLookup.Empty;

        var latestRankRunId = await ResolveLatestParserRunIdAsync(RanksKind, cancellationToken);
        var ranks = latestRankRunId is null
            ? new Dictionary<Guid, ParserProductRankSummaryDto>()
            : await LoadRankSummariesAsync(products, latestRankRunId, cancellationToken);
        var positions = latestRankRunId is null
            ? BuildUnknownPositions(products)
            : await LoadPositionSummariesAsync(products, latestRankRunId, ranks, cancellationToken);
        var reviews = await LoadReviewEvidenceAsync(products, cancellationToken);
        var latestLogisticsRunId = await ResolveLatestParserRunIdAsync(LogisticsKind, cancellationToken);
        var logistics = latestLogisticsRunId is null
            ? ProductLogisticsEvidence.Empty
            : await LoadLogisticsEvidenceAsync(products, latestLogisticsRunId, includeLogisticsDetail, cancellationToken);
        return new ProductEvidenceLookup(ranks, positions, reviews, logistics.Summaries, logistics.Details);
    }

    private async Task<IReadOnlyDictionary<Guid, ParserProductRankSummaryDto>> LoadRankSummariesAsync(
        IReadOnlyList<ParserProductRow> products,
        string latestRankRunId,
        CancellationToken cancellationToken)
    {
        var productIds = products
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        var rootIds = products
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct()
            .ToList();
        var rootProductCounts = products
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

        var candidates = new List<RankCandidate>();
        if (productIds.Count > 0)
        {
            candidates.AddRange(await _dbContext.ParserRankSnapshotRows
                .AsNoTracking()
                .Where(x =>
                    x.ParserRunId == latestRankRunId
                    && productIds.Contains(x.WbProductId))
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
            if (summariesByMatchKey.TryGetValue(new RankMatchKey(false, product.WbProductId), out var exactSummary))
            {
                result[product.Id] = exactSummary;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(product.WbRootId)
                && rootProductCounts.GetValueOrDefault(product.WbRootId) == 1
                && summariesByMatchKey.TryGetValue(new RankMatchKey(true, product.WbRootId), out var rootSummary))
            {
                result[product.Id] = rootSummary;
            }
            else if (string.IsNullOrWhiteSpace(product.WbRootId)
                && summariesByMatchKey.TryGetValue(new RankMatchKey(false, product.WbProductId), out var summary))
            {
                result[product.Id] = summary;
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, ParserProductPositionDto>> LoadPositionSummariesAsync(
        IReadOnlyList<ParserProductRow> products,
        string latestRankRunId,
        IReadOnlyDictionary<Guid, ParserProductRankSummaryDto> ranks,
        CancellationToken cancellationToken)
    {
        var coverageRows = await _dbContext.ParserRankSnapshotRows
            .AsNoTracking()
            .Where(x => x.ParserRunId == latestRankRunId)
            .Select(x => new PositionCoverageCandidate(
                x.SourceCategory,
                x.SourceSubcategory,
                x.SourceRegionDest,
                x.WbRootId,
                x.WbProductId,
                x.Query,
                x.AbsolutePosition,
                x.ObservedAtUtc))
            .ToListAsync(cancellationToken);
        var rankedRootIds = coverageRows
            .Select(x => x.WbRootId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var exactRankedProductIds = coverageRows
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        var duplicateRootIds = products
            .Where(x => !string.IsNullOrWhiteSpace(x.WbRootId))
            .GroupBy(x => x.WbRootId!)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        var coverageByKey = coverageRows
            .GroupBy(x => new PositionCoverageKey(x.SourceCategory, x.SourceSubcategory, x.SourceRegionDest))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var queries = x
                        .Select(candidate => candidate.Query)
                        .Where(query => !string.IsNullOrWhiteSpace(query))
                        .Distinct(StringComparer.Ordinal)
                        .Take(2)
                        .ToList();

                    return new PositionCoverage(
                        ObservedRangeLimit: x.Max(candidate => candidate.AbsolutePosition),
                        Query: queries.Count == 1 ? queries[0] : null,
                        ObservedAtUtc: x.Max(candidate => candidate.ObservedAtUtc));
                });

        var result = new Dictionary<Guid, ParserProductPositionDto>();
        foreach (var product in products)
        {
            if (ranks.TryGetValue(product.Id, out var rank))
            {
                result[product.Id] = new ParserProductPositionDto(
                    PositionStateObserved,
                    rank.AbsolutePosition,
                    null,
                    rank.Query,
                    rank.SourceCategory,
                    rank.SourceSubcategory,
                    rank.ObservedAtUtc);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(product.WbRootId)
                && duplicateRootIds.Contains(product.WbRootId)
                && rankedRootIds.Contains(product.WbRootId)
                && !exactRankedProductIds.Contains(product.WbProductId))
            {
                result[product.Id] = UnknownPosition(product);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(product.SourceSubcategory)
                && coverageByKey.TryGetValue(
                    new PositionCoverageKey(product.SourceCategory, product.SourceSubcategory, product.SourceRegionDest),
                    out var coverage))
            {
                result[product.Id] = new ParserProductPositionDto(
                    PositionStateBeyondObservedRange,
                    null,
                    coverage.ObservedRangeLimit,
                    coverage.Query,
                    product.SourceCategory,
                    product.SourceSubcategory,
                    coverage.ObservedAtUtc);
                continue;
            }

            result[product.Id] = UnknownPosition(product);
        }

        return result;
    }

    private static IReadOnlyDictionary<Guid, ParserProductPositionDto> BuildUnknownPositions(
        IReadOnlyList<ParserProductRow> products)
    {
        return products.ToDictionary(product => product.Id, UnknownPosition);
    }

    private static ParserProductPositionDto UnknownPosition(ParserProductRow product)
    {
        return new ParserProductPositionDto(
            PositionStateUnknown,
            null,
            null,
            null,
            product.SourceCategory,
            product.SourceSubcategory,
            null);
    }

    private async Task<ProductLogisticsEvidence> LoadLogisticsEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        string latestLogisticsRunId,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        var productIds = products
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (productIds.Count == 0)
            return ProductLogisticsEvidence.Empty;

        var snapshots = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId == latestLogisticsRunId
                && productIds.Contains(x.WbProductId))
            .Select(x => new LogisticsSnapshotCandidate(
                x.SourceLineNumber,
                x.ParserRunId,
                x.ObservedAtUtc,
                x.SourceRegionDest,
                x.WbProductId,
                x.TotalQuantityObserved,
                x.QuantityIsCapped,
                x.QuantityCapObserved,
                x.QuantitySemantics,
                x.ProductWhRaw,
                x.ProductTime1Raw,
                x.ProductTime2Raw,
                x.ProductDtypeRaw,
                x.ProductDistRaw))
            .ToListAsync(cancellationToken);
        if (snapshots.Count == 0)
            return ProductLogisticsEvidence.Empty;

        var warehouseRows = await _dbContext.ParserWarehouseAvailabilityRows
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId == latestLogisticsRunId
                && productIds.Contains(x.WbProductId))
            .Select(x => new WarehouseAvailabilityCandidate(
                x.SourceLineNumber,
                x.ParserRunId,
                x.SourceRegionDest,
                x.WbProductId,
                x.WarehouseIdOnMp,
                x.OptionId,
                x.SizeName,
                x.SizeOrigName,
                x.QuantityObserved,
                x.QuantityIsCapped,
                x.QuantityCapObserved,
                x.QuantitySemantics,
                x.StockPriorityRaw,
                x.StockTime1Raw,
                x.StockTime2Raw,
                x.StockDtypeRaw,
                x.StockDistRaw,
                x.PriceBasic,
                x.PriceProduct,
                x.PriceLogisticsRaw,
                x.PriceReturnRaw))
            .ToListAsync(cancellationToken);

        var warehouseByProductAndDestination = warehouseRows
            .GroupBy(x => new LogisticsWarehouseKey(x.WbProductId, x.SourceRegionDest))
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(row => row.SourceLineNumber).ToList());

        var snapshotsByProductId = snapshots
            .GroupBy(x => x.WbProductId)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var summaries = new Dictionary<Guid, ParserProductLogisticsSummaryDto>();
        var details = includeDetails
            ? new Dictionary<Guid, ParserProductLogisticsDetailDto>()
            : new Dictionary<Guid, ParserProductLogisticsDetailDto>();

        foreach (var product in products)
        {
            if (!snapshotsByProductId.TryGetValue(product.WbProductId, out var candidates))
                continue;

            var selected = SelectLogisticsSnapshot(product, candidates);
            if (selected is null)
                continue;

            var warehouseKey = new LogisticsWarehouseKey(selected.WbProductId, selected.SourceRegionDest);
            warehouseByProductAndDestination.TryGetValue(warehouseKey, out var selectedWarehouseRows);
            selectedWarehouseRows ??= [];

            var warehouseCount = selectedWarehouseRows
                .Select(x => x.WarehouseIdOnMp)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Count();
            var summary = MapLogisticsSummary(selected, warehouseCount, selectedWarehouseRows.Count > 0);
            summaries[product.Id] = summary;

            if (includeDetails)
            {
                details[product.Id] = new ParserProductLogisticsDetailDto(
                    summary,
                    selected.ProductWhRaw,
                    selected.ProductTime1Raw,
                    selected.ProductTime2Raw,
                    selected.ProductDtypeRaw,
                    selected.ProductDistRaw,
                    selectedWarehouseRows.Select(MapWarehouseAvailability).ToList());
            }
        }

        return new ProductLogisticsEvidence(summaries, details);
    }

    private static LogisticsSnapshotCandidate? SelectLogisticsSnapshot(
        ParserProductRow product,
        IReadOnlyList<LogisticsSnapshotCandidate> candidates)
    {
        var scopedCandidates = candidates;
        if (!string.IsNullOrWhiteSpace(product.SourceRegionDest))
        {
            var sameDestination = candidates
                .Where(x => x.SourceRegionDest == product.SourceRegionDest)
                .ToList();
            if (sameDestination.Count > 0)
                scopedCandidates = sameDestination;
        }

        return scopedCandidates
            .OrderByDescending(x => x.ObservedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .FirstOrDefault();
    }

    private static ParserProductLogisticsSummaryDto MapLogisticsSummary(
        LogisticsSnapshotCandidate row,
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
            row.ParserRunId,
            row.ObservedAtUtc,
            hasWarehouseRows);
    }

    private static string QuantityLabel(
        int? totalQuantityObserved,
        bool? quantityIsCapped,
        int? quantityCapObserved)
    {
        if (quantityIsCapped == true && quantityCapObserved.HasValue)
            return $"≥{quantityCapObserved.Value.ToString(CultureInfo.InvariantCulture)}";

        return totalQuantityObserved.HasValue
            ? totalQuantityObserved.Value.ToString(CultureInfo.InvariantCulture)
            : "Нет данных";
    }

    private static ParserWarehouseAvailabilityDto MapWarehouseAvailability(WarehouseAvailabilityCandidate row)
    {
        return new ParserWarehouseAvailabilityDto(
            row.WarehouseIdOnMp,
            row.OptionId,
            row.SizeName,
            row.SizeOrigName,
            row.QuantityObserved,
            row.QuantityIsCapped,
            row.QuantityCapObserved,
            row.QuantitySemantics,
            row.StockPriorityRaw,
            row.StockTime1Raw,
            row.StockTime2Raw,
            row.StockDtypeRaw,
            row.StockDistRaw,
            row.PriceBasic,
            row.PriceProduct,
            row.PriceLogisticsRaw,
            row.PriceReturnRaw);
    }

    private async Task<IReadOnlyList<SelectedLogisticsObservation>> LoadSelectedLogisticsObservationsAsync(
        IReadOnlyList<ParserProductRow> products,
        string latestLogisticsRunId,
        CancellationToken cancellationToken)
    {
        var productIds = products
            .Select(x => x.WbProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (productIds.Count == 0)
            return [];

        var snapshots = await _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId == latestLogisticsRunId
                && productIds.Contains(x.WbProductId))
            .Select(x => new LogisticsSnapshotCandidate(
                x.SourceLineNumber,
                x.ParserRunId,
                x.ObservedAtUtc,
                x.SourceRegionDest,
                x.WbProductId,
                x.TotalQuantityObserved,
                x.QuantityIsCapped,
                x.QuantityCapObserved,
                x.QuantitySemantics,
                x.ProductWhRaw,
                x.ProductTime1Raw,
                x.ProductTime2Raw,
                x.ProductDtypeRaw,
                x.ProductDistRaw))
            .ToListAsync(cancellationToken);
        if (snapshots.Count == 0)
            return [];

        var warehouseRows = await _dbContext.ParserWarehouseAvailabilityRows
            .AsNoTracking()
            .Where(x =>
                x.ParserRunId == latestLogisticsRunId
                && productIds.Contains(x.WbProductId))
            .Select(x => new WarehouseAvailabilityCandidate(
                x.SourceLineNumber,
                x.ParserRunId,
                x.SourceRegionDest,
                x.WbProductId,
                x.WarehouseIdOnMp,
                x.OptionId,
                x.SizeName,
                x.SizeOrigName,
                x.QuantityObserved,
                x.QuantityIsCapped,
                x.QuantityCapObserved,
                x.QuantitySemantics,
                x.StockPriorityRaw,
                x.StockTime1Raw,
                x.StockTime2Raw,
                x.StockDtypeRaw,
                x.StockDistRaw,
                x.PriceBasic,
                x.PriceProduct,
                x.PriceLogisticsRaw,
                x.PriceReturnRaw))
            .ToListAsync(cancellationToken);

        var warehouseByProductAndDestination = warehouseRows
            .GroupBy(x => new LogisticsWarehouseKey(x.WbProductId, x.SourceRegionDest))
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(row => row.SourceLineNumber).ToList());

        var snapshotsByProductId = snapshots
            .GroupBy(x => x.WbProductId)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);

        var observations = new List<SelectedLogisticsObservation>();
        foreach (var product in products)
        {
            if (!snapshotsByProductId.TryGetValue(product.WbProductId, out var candidates))
                continue;

            var selected = SelectLogisticsSnapshot(product, candidates);
            if (selected is null)
                continue;

            var warehouseKey = new LogisticsWarehouseKey(selected.WbProductId, selected.SourceRegionDest);
            warehouseByProductAndDestination.TryGetValue(warehouseKey, out var selectedWarehouseRows);
            selectedWarehouseRows ??= [];

            observations.Add(new SelectedLogisticsObservation(
                product.Id,
                selected,
                selectedWarehouseRows));
        }

        return observations;
    }

    private static ParserProductLogisticsSummaryAggregateDto BuildLogisticsAggregate(
        string? productRunId,
        string? logisticsRunId,
        ParserProductLogisticsSummaryQuery query,
        int productsTotal,
        IReadOnlyList<SelectedLogisticsObservation> observations)
    {
        var productsWithLogistics = observations.Count;
        var productsWithoutLogistics = productsTotal - productsWithLogistics;
        var quantities = observations
            .Select(x => x.Snapshot.TotalQuantityObserved)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(x => x)
            .ToList();
        var productsWithQuantity = quantities.Count;
        var productsWithoutQuantity = productsTotal - productsWithQuantity;
        var buckets = BuildQuantityBuckets(observations, productsWithoutQuantity);
        var warehouseRowsTotal = observations.Sum(x => x.WarehouseRows.Count);
        var distinctWarehouseIds = observations
            .SelectMany(x => x.WarehouseRows)
            .Select(x => x.WarehouseIdOnMp)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Count();
        var productsWithWarehouseRows = observations.Count(x => x.WarehouseRows.Count > 0);
        var distinctWarehouseCountSum = observations.Sum(x => x.DistinctWarehouseCount);
        var averageWarehousesPerProduct = productsWithLogistics == 0
            ? 0m
            : RoundMetric((decimal)distinctWarehouseCountSum / productsWithLogistics);
        var destinations = observations
            .GroupBy(x => x.Snapshot.SourceRegionDest)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new ParserProductLogisticsDestinationSummaryDto(
                x.Key,
                x.Count(),
                x.Max(row => row.Snapshot.ObservedAtUtc)))
            .ToList();
        var latestObservedAtUtc = observations.Count == 0
            ? (DateTime?)null
            : observations.Max(x => x.Snapshot.ObservedAtUtc);

        return new ParserProductLogisticsSummaryAggregateDto(
            productRunId,
            logisticsRunId,
            NormalizeQueryValue(query.SourceCategory),
            NormalizeQueryValue(query.SourceSubcategory),
            productsTotal,
            productsWithLogistics,
            productsWithoutLogistics,
            productsWithQuantity,
            productsWithoutQuantity,
            quantities.Count == 0 ? null : quantities.Min(),
            quantities.Count == 0 ? null : quantities.Max(),
            quantities.Count == 0 ? null : RoundMetric(quantities.Average()),
            Median(quantities),
            buckets,
            productsWithWarehouseRows,
            warehouseRowsTotal,
            distinctWarehouseIds,
            averageWarehousesPerProduct,
            destinations,
            latestObservedAtUtc,
            BuildLogisticsAggregateWarnings(productsTotal, productsWithLogistics, logisticsRunId));
    }

    private static ParserProductQuantityBucketsDto BuildQuantityBuckets(
        IReadOnlyList<SelectedLogisticsObservation> observations,
        int unknown)
    {
        var zero = 0;
        var oneToFive = 0;
        var sixToTwenty = 0;
        var twentyOneToThirtyNine = 0;
        var fortyPlusOrHigh = 0;

        foreach (var quantity in observations.Select(x => x.Snapshot.TotalQuantityObserved).Where(x => x.HasValue).Select(x => x!.Value))
        {
            if (quantity == 0)
                zero++;
            else if (quantity <= 5)
                oneToFive++;
            else if (quantity <= 20)
                sixToTwenty++;
            else if (quantity <= 39)
                twentyOneToThirtyNine++;
            else
                fortyPlusOrHigh++;
        }

        return new ParserProductQuantityBucketsDto(
            zero,
            oneToFive,
            sixToTwenty,
            twentyOneToThirtyNine,
            fortyPlusOrHigh,
            unknown);
    }

    private static IReadOnlyList<string> BuildLogisticsAggregateWarnings(
        int productsTotal,
        int productsWithLogistics,
        string? logisticsRunId)
    {
        var warnings = new List<string>
        {
            WarningQuantityExactnessNotProven,
            WarningWarehouseIdsAreExternalMarketplaceIds
        };

        if (string.IsNullOrWhiteSpace(logisticsRunId))
            warnings.Add(WarningLatestLogisticsRunNotFound);

        if (productsTotal > 0 && productsWithLogistics < productsTotal)
            warnings.Add(WarningLatestProductsAndLogisticsOverlapIsPartial);

        return warnings;
    }

    private static decimal? Median(IReadOnlyList<int> sortedValues)
    {
        if (sortedValues.Count == 0)
            return null;

        var midpoint = sortedValues.Count / 2;
        if (sortedValues.Count % 2 == 1)
            return sortedValues[midpoint];

        return RoundMetric((sortedValues[midpoint - 1] + sortedValues[midpoint]) / 2m);
    }

    private static decimal RoundMetric(double value)
    {
        return RoundMetric((decimal)value);
    }

    private static decimal RoundMetric(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeQueryValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static IQueryable<ParserProductRow> ApplyLogisticsSummaryFilters(
        IQueryable<ParserProductRow> rows,
        ParserProductLogisticsSummaryQuery query)
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

    private async Task<(int TotalCount, List<ParserProductRow> Rows)> LoadPositionSortedRowsAsync(
        IQueryable<ParserProductRow> rows,
        int page,
        int pageSize,
        bool descending,
        CancellationToken cancellationToken)
    {
        var totalCount = await rows.CountAsync(cancellationToken);
        if (totalCount == 0)
            return (0, []);

        var allRows = await rows.ToListAsync(cancellationToken);
        var latestRankRunId = await ResolveLatestParserRunIdAsync(RanksKind, cancellationToken);
        var ranks = latestRankRunId is null
            ? new Dictionary<Guid, ParserProductRankSummaryDto>()
            : await LoadRankSummariesAsync(allRows, latestRankRunId, cancellationToken);
        var positions = latestRankRunId is null
            ? BuildUnknownPositions(allRows)
            : await LoadPositionSummariesAsync(allRows, latestRankRunId, ranks, cancellationToken);

        var orderedRows = descending
            ? allRows
                .OrderBy(row => PositionStateOrder(positions.GetValueOrDefault(row.Id), descending: true))
                .ThenByDescending(row => PositionNumericOrder(positions.GetValueOrDefault(row.Id)))
                .ThenByDescending(row => row.SourceLineNumber)
                .ThenByDescending(row => row.Id)
            : allRows
                .OrderBy(row => PositionStateOrder(positions.GetValueOrDefault(row.Id), descending: false))
                .ThenBy(row => PositionNumericOrder(positions.GetValueOrDefault(row.Id)))
                .ThenBy(row => row.SourceLineNumber)
                .ThenBy(row => row.Id);

        return (totalCount, orderedRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList());
    }

    private static bool IsPositionSort(string? sort)
    {
        var value = sort?.Trim();
        return string.Equals(value, "position", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "-position", StringComparison.OrdinalIgnoreCase);
    }

    private static int PositionStateOrder(ParserProductPositionDto? position, bool descending)
    {
        return position?.State switch
        {
            PositionStateObserved => descending ? 2 : 0,
            PositionStateBeyondObservedRange => 1,
            _ => descending ? 0 : 2
        };
    }

    private static int PositionNumericOrder(ParserProductPositionDto? position)
    {
        return position?.AbsolutePosition
            ?? position?.ObservedRangeLimit
            ?? 0;
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
            "price" or "priceDiscounted" => rows.OrderBy(x => x.PriceDiscounted == null).ThenBy(x => x.PriceDiscounted).ThenBy(x => x.SourceLineNumber).ThenBy(x => x.Id),
            "-price" or "-priceDiscounted" => rows.OrderBy(x => x.PriceDiscounted == null).ThenByDescending(x => x.PriceDiscounted).ThenByDescending(x => x.SourceLineNumber).ThenByDescending(x => x.Id),
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
            row.TotalQuantity,
            row.RatingRounded,
            row.ReviewRating,
            row.FeedbackCount,
            row.SourceCategory,
            row.SourceSubcategory,
            row.SourceQuery,
            GetImageUrls(row.ImageUrls).FirstOrDefault(),
            evidence.GetRank(row.Id),
            evidence.GetPosition(row.Id),
            evidence.GetLogisticsSummary(row.Id),
            evidence.GetReviewEvidence(row.Id));
    }

    private static ParserProductDetailDto MapToDetail(
        ParserProductRow row,
        ParserFile sourceFile,
        ProductEvidenceLookup evidence,
        ParserProductDetailRow? details)
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
            evidence.GetPosition(row.Id),
            evidence.GetLogisticsSummary(row.Id),
            evidence.GetLogisticsDetail(row.Id),
            Description: details?.Description,
            Characteristics: details?.Characteristics?.RootElement.Clone(),
            VisualAnalysis: null,
            ParsedReviewEvidence: evidence.GetReviewEvidence(row.Id));
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
        IReadOnlyDictionary<Guid, ParserProductPositionDto> Positions,
        IReadOnlyDictionary<Guid, ParserProductReviewEvidenceDto> Reviews,
        IReadOnlyDictionary<Guid, ParserProductLogisticsSummaryDto> LogisticsSummaries,
        IReadOnlyDictionary<Guid, ParserProductLogisticsDetailDto> LogisticsDetails)
    {
        public static ProductEvidenceLookup Empty { get; } = new(
            new Dictionary<Guid, ParserProductRankSummaryDto>(),
            new Dictionary<Guid, ParserProductPositionDto>(),
            new Dictionary<Guid, ParserProductReviewEvidenceDto>(),
            new Dictionary<Guid, ParserProductLogisticsSummaryDto>(),
            new Dictionary<Guid, ParserProductLogisticsDetailDto>());

        public ParserProductRankSummaryDto? GetRank(Guid productId)
        {
            return Ranks.TryGetValue(productId, out var rank) ? rank : null;
        }

        public ParserProductPositionDto? GetPosition(Guid productId)
        {
            return Positions.TryGetValue(productId, out var position) ? position : null;
        }

        public ParserProductReviewEvidenceDto GetReviewEvidence(Guid productId)
        {
            return Reviews.TryGetValue(productId, out var evidence) ? evidence : EmptyReviewEvidence;
        }

        public ParserProductLogisticsSummaryDto? GetLogisticsSummary(Guid productId)
        {
            return LogisticsSummaries.TryGetValue(productId, out var logistics) ? logistics : null;
        }

        public ParserProductLogisticsDetailDto? GetLogisticsDetail(Guid productId)
        {
            return LogisticsDetails.TryGetValue(productId, out var logistics) ? logistics : null;
        }
    }

    private sealed record ProductLogisticsEvidence(
        IReadOnlyDictionary<Guid, ParserProductLogisticsSummaryDto> Summaries,
        IReadOnlyDictionary<Guid, ParserProductLogisticsDetailDto> Details)
    {
        public static ProductLogisticsEvidence Empty { get; } = new(
            new Dictionary<Guid, ParserProductLogisticsSummaryDto>(),
            new Dictionary<Guid, ParserProductLogisticsDetailDto>());
    }

    private sealed record SelectedLogisticsObservation(
        Guid ProductRowId,
        LogisticsSnapshotCandidate Snapshot,
        IReadOnlyList<WarehouseAvailabilityCandidate> WarehouseRows)
    {
        public int DistinctWarehouseCount { get; } = WarehouseRows
            .Select(x => x.WarehouseIdOnMp)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private sealed record LogisticsSnapshotCandidate(
        long SourceLineNumber,
        string ParserRunId,
        DateTime ObservedAtUtc,
        string SourceRegionDest,
        string WbProductId,
        int? TotalQuantityObserved,
        bool? QuantityIsCapped,
        int? QuantityCapObserved,
        string QuantitySemantics,
        string? ProductWhRaw,
        int? ProductTime1Raw,
        int? ProductTime2Raw,
        long? ProductDtypeRaw,
        int? ProductDistRaw);

    private sealed record WarehouseAvailabilityCandidate(
        long SourceLineNumber,
        string ParserRunId,
        string SourceRegionDest,
        string WbProductId,
        string? WarehouseIdOnMp,
        string? OptionId,
        string? SizeName,
        string? SizeOrigName,
        int? QuantityObserved,
        bool? QuantityIsCapped,
        int? QuantityCapObserved,
        string QuantitySemantics,
        int? StockPriorityRaw,
        int? StockTime1Raw,
        int? StockTime2Raw,
        long? StockDtypeRaw,
        int? StockDistRaw,
        decimal? PriceBasic,
        decimal? PriceProduct,
        decimal? PriceLogisticsRaw,
        decimal? PriceReturnRaw);

    private readonly record struct LogisticsWarehouseKey(string WbProductId, string SourceRegionDest);

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

    private sealed record PositionCoverageCandidate(
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest,
        string? WbRootId,
        string WbProductId,
        string Query,
        int AbsolutePosition,
        DateTime ObservedAtUtc);

    private sealed record PositionCoverage(
        int ObservedRangeLimit,
        string? Query,
        DateTime ObservedAtUtc);

    private readonly record struct PositionCoverageKey(
        string? SourceCategory,
        string? SourceSubcategory,
        string? SourceRegionDest);

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
