using System.Globalization;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public sealed partial class ParserProductReadService : IParserProductReadService
{
    private static readonly SemaphoreSlim ProductFilterOptionsCacheLock = new(1, 1);
    private static readonly TimeSpan ProductFilterOptionsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim DemoCardOptionsCacheLock = new(1, 1);
    private static readonly TimeSpan DemoCardOptionsCacheDuration = TimeSpan.FromMinutes(15);
    private static ParserProductFilterOptionsDto? _productionProductFilterOptionsCache;
    private static DateTime _productionProductFilterOptionsCacheExpiresAtUtc;
    private static ParserDemoCardOptionsDto? _demoCardOptionsCache;
    private static ParserDemoCardOptionsDto? _demoCardBaseOptionsCache;
    private static DateTime _demoCardOptionsCacheExpiresAtUtc;
    private static DateTime _demoCardBaseOptionsCacheExpiresAtUtc;
    private static readonly Dictionary<string, (ParserDemoCardCharacteristicsDto Value, DateTime ExpiresAtUtc)> DemoCardCharacteristicsCache = new(StringComparer.Ordinal);

    private const string ProductsKind = "products";
    private const string RanksKind = "ranks";
    private const string LogisticsKind = "logistics";
    private const string ProductDetailsKind = "product_details";
    private const string SucceededStatus = "succeeded";
    private const string PartialStatus = "partial";
    private const string DefaultAttributionMode = "root_payload";
    private const string PositionStateObserved = "observed";
    private const string PositionStateBeyondObservedRange = "beyondObservedRange";
    private const string PositionStateUnknown = "unknown";
    private const string WarningQuantityExactnessNotProven = "quantity_exactness_not_proven";
    private const string WarningWarehouseIdsAreExternalMarketplaceIds = "warehouse_ids_are_external_marketplace_ids";
    private const string WarningLatestProductsAndLogisticsOverlapIsPartial = "latest_products_and_logistics_overlap_is_partial";
    private const string WarningLatestLogisticsRunNotFound = "latest_logistics_run_not_found";
    private const string MoscowDeliveryDestination = "1259570991";
    private const string MoscowDeliveryCity = "РњРѕСЃРєРІР°";
    private const string MoscowDeliveryLabel = "РњРѕСЃРєРІР°, РџР’Р— WB РЅР° СѓР»РёС†Рµ Р—Р°С†РµРїР° 32";
    private const string MoscowDeliveryAddress = "Рі РњРѕСЃРєРІР°, СѓР»РёС†Р° Р—Р°С†РµРїР° 32";

    private static readonly string[] DemoCardSubcategoryAllowlist =
    [
        "РћСЂРіР°РЅР°Р№Р·РµСЂС‹ РґР»СЏ С…СЂР°РЅРµРЅРёСЏ РІРµС‰РµР№",
        "РљРѕРІСЂРёРєРё РґР»СЏ РІР°РЅРЅРѕР№",
        "РЎРІРµС‚РёР»СЊРЅРёРєРё Р±СЂР°"
    ];
    private static readonly IReadOnlyDictionary<string, string> DemoCardCategoryBySubcategory =
        DemoCardSubcategoryAllowlist.ToDictionary(
            subcategory => subcategory,
            _ => "РўРѕРІР°СЂС‹ РґР»СЏ РґРѕРјР°",
            StringComparer.Ordinal);

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
        var runScope = ParserRunScope.From(query.IncludeTestRuns, query.TestRunsOnly, query.TestLabel);
        if (CanUseCurrentProductRows(query, runScope))
            return await GetCurrentProductListAsync(query, page, pageSize, cancellationToken);

        var rows = await BuildEffectiveProductRowsAsync(query.ParserRunId, runScope, cancellationToken);
        if (rows is null)
        {
            return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
                new PagedResponse<ParserProductListItemDto>([], page, pageSize, 0));
        }

        rows = ApplyFilters(rows, query);
        rows = await ApplyRequireDeliveryProfileFilterAsync(rows, query, runScope, cancellationToken);

        int totalCount;
        List<ParserProductRow> pageRows;
        if (IsPositionSort(query.Sort))
        {
            (totalCount, pageRows) = await LoadPositionSortedRowsAsync(
                rows,
                page,
                pageSize,
                query.Sort!.Trim().StartsWith("-", StringComparison.Ordinal),
                runScope,
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

        var evidence = await LoadListEvidenceAsync(pageRows, runScope, cancellationToken);
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
        var runScope = ParserRunScope.From(query.IncludeTestRuns, query.TestRunsOnly, query.TestLabel);
        if (CanUseCurrentProductFilterOptions(query, runScope))
            return await GetCurrentProductFilterOptionsAsync(query, cancellationToken);

        if (CanUseProductionFilterOptionsCache(query))
        {
            var cached = await GetCachedProductionFilterOptionsAsync(runScope, cancellationToken);
            if (cached is not null)
                return ServiceResult<ParserProductFilterOptionsDto>.Success(cached);
        }

        var rows = await BuildEffectiveProductRowsAsync(query.ParserRunId, runScope, cancellationToken);
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

    public async Task<ServiceResult<ParserDemoCardOptionsDto>> GetDemoCardOptionsAsync(
        bool includeCharacteristics,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cachedOptions = includeCharacteristics ? _demoCardOptionsCache : _demoCardBaseOptionsCache;
        var cacheExpiresAtUtc = includeCharacteristics ? _demoCardOptionsCacheExpiresAtUtc : _demoCardBaseOptionsCacheExpiresAtUtc;
        if (cachedOptions is not null && cacheExpiresAtUtc > now)
        {
            return ServiceResult<ParserDemoCardOptionsDto>.Success(cachedOptions);
        }

        await DemoCardOptionsCacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTime.UtcNow;
            cachedOptions = includeCharacteristics ? _demoCardOptionsCache : _demoCardBaseOptionsCache;
            cacheExpiresAtUtc = includeCharacteristics ? _demoCardOptionsCacheExpiresAtUtc : _demoCardBaseOptionsCacheExpiresAtUtc;
            if (cachedOptions is not null && cacheExpiresAtUtc > now)
            {
                return ServiceResult<ParserDemoCardOptionsDto>.Success(cachedOptions);
            }

            var options = await BuildDemoCardOptionsAsync(includeCharacteristics, cancellationToken);
            if (includeCharacteristics)
            {
                _demoCardOptionsCache = options;
                _demoCardOptionsCacheExpiresAtUtc = now.Add(DemoCardOptionsCacheDuration);
            }
            else
            {
                _demoCardBaseOptionsCache = options;
                _demoCardBaseOptionsCacheExpiresAtUtc = now.Add(DemoCardOptionsCacheDuration);
            }

            return ServiceResult<ParserDemoCardOptionsDto>.Success(options);
        }
        finally
        {
            DemoCardOptionsCacheLock.Release();
        }
    }

    public async Task<ServiceResult<ParserDemoCardCharacteristicsDto>> GetDemoCardCharacteristicsAsync(
        string subcategory,
        CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(subcategory) ? string.Empty : subcategory.Trim();
        if (normalized.Length == 0)
        {
            return ServiceResult<ParserDemoCardCharacteristicsDto>.BadRequest("Subcategory is required.");
        }

        if (!DemoCardSubcategoryAllowlist.Contains(normalized, StringComparer.Ordinal))
        {
            return ServiceResult<ParserDemoCardCharacteristicsDto>.Success(
                new ParserDemoCardCharacteristicsDto(normalized, []));
        }

        var now = DateTime.UtcNow;
        if (DemoCardCharacteristicsCache.TryGetValue(normalized, out var cached)
            && cached.ExpiresAtUtc > now)
        {
            return ServiceResult<ParserDemoCardCharacteristicsDto>.Success(cached.Value);
        }

        await DemoCardOptionsCacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTime.UtcNow;
            if (DemoCardCharacteristicsCache.TryGetValue(normalized, out cached)
                && cached.ExpiresAtUtc > now)
            {
                return ServiceResult<ParserDemoCardCharacteristicsDto>.Success(cached.Value);
            }

            var characteristics = await LoadDemoCardCharacteristicsAsync(normalized, cancellationToken);
            DemoCardCharacteristicsCache[normalized] = (
                characteristics,
                now.Add(DemoCardOptionsCacheDuration));

            return ServiceResult<ParserDemoCardCharacteristicsDto>.Success(characteristics);
        }
        finally
        {
            DemoCardOptionsCacheLock.Release();
        }
    }

    private async Task<ParserDemoCardOptionsDto> BuildDemoCardOptionsAsync(
        bool includeCharacteristics,
        CancellationToken cancellationToken)
    {
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: true);

        var categories = DemoCardCategoryBySubcategory.Values
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(comparer)
            .OrderBy(x => x, comparer)
            .ToList();

        var subcategoriesByCategory = DemoCardCategoryBySubcategory
            .GroupBy(x => x.Value.Trim(), comparer)
            .Select(group => new ParserDemoCardSubcategoriesDto(
                group.Key,
                group
                    .Select(x => x.Key.Trim())
                    .Where(x => x.Length > 0)
                    .Distinct(comparer)
                    .OrderBy(x => x, comparer)
                    .ToList()))
            .OrderBy(x => x.Category, comparer)
            .ToList();

        var characteristicsBySubcategory = new List<ParserDemoCardCharacteristicsDto>();
        if (includeCharacteristics)
        {
            foreach (var subcategory in DemoCardSubcategoryAllowlist)
            {
                characteristicsBySubcategory.Add(await LoadDemoCardCharacteristicsAsync(subcategory, cancellationToken));
            }

            characteristicsBySubcategory = characteristicsBySubcategory
                .OrderBy(x => x.Subcategory, comparer)
                .ToList();
        }

        return new ParserDemoCardOptionsDto(categories, subcategoriesByCategory, characteristicsBySubcategory);
    }

    private async Task<ParserDemoCardCharacteristicsDto> LoadDemoCardCharacteristicsAsync(
        string subcategory,
        CancellationToken cancellationToken)
    {
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: true);
        var detailRows = await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x =>
                x.Status == SucceededStatus
                && x.SourceSubcategory == subcategory
                && x.Characteristics != null)
            .OrderByDescending(x => x.ParsedAtUtc)
            .Take(50)
            .Select(x => new DemoCardDetailCharacteristicsRow(
                x.SourceSubcategory!,
                x.Characteristics))
            .ToListAsync(cancellationToken);

        var names = detailRows
            .SelectMany(x => ExtractCharacteristicNames(x.Characteristics))
            .Distinct(comparer)
            .OrderBy(x => x, comparer)
            .ToList();

        return new ParserDemoCardCharacteristicsDto(subcategory, names);
    }

    public async Task<ServiceResult<ParserProductLogisticsSummaryAggregateDto>> GetLogisticsSummaryAsync(
        ParserProductLogisticsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var runScope = ParserRunScope.From(query.IncludeTestRuns, query.TestRunsOnly, query.TestLabel);
        var productRunId = await ResolveEffectiveProductRunIdAsync(query.ParserRunId, runScope, cancellationToken);
        var latestLogisticsRunId = await ResolveLatestParserRunIdAsync(LogisticsKind, runScope, cancellationToken);

        if (productRunId is null)
        {
            return ServiceResult<ParserProductLogisticsSummaryAggregateDto>.Success(
                BuildLogisticsAggregate(
                    productRunId: null,
                    logisticsRunId: latestLogisticsRunId,
                    query,
                    productsTotal: 0,
                    observations: [],
                    destinationSummaries: []));
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
                    observations: [],
                    destinationSummaries: []));
        }

        var observations = await LoadSelectedLogisticsObservationsAsync(
            products,
            latestLogisticsRunId,
            cancellationToken);
        var destinationSummaries = await LoadLogisticsDestinationSummariesAsync(
            products,
            latestLogisticsRunId,
            cancellationToken);

        return ServiceResult<ParserProductLogisticsSummaryAggregateDto>.Success(
            BuildLogisticsAggregate(
                productRunId,
                latestLogisticsRunId,
                query,
                products.Count,
                observations,
                destinationSummaries));
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
        var runScope = await LoadScopeForParserRunAsync(row.ParserRunId, cancellationToken);
        var evidence = await LoadEvidenceAsync([row], includeLogisticsDetail: true, runScope, cancellationToken);
        var details = await LoadProductDetailAsync(row, cancellationToken);

        return ServiceResult<ParserProductDetailDto>.Success(MapToDetail(row, sourceFile, evidence, details));
    }

    private async Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetCurrentProductListAsync(
        ParserProductListQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = _dbContext.ParserCurrentProductRows.AsNoTracking();
        rows = ApplyCurrentFilters(rows, query);

        var totalCount = await rows.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
                new PagedResponse<ParserProductListItemDto>([], page, pageSize, 0));
        }

        var pageProductIds = await ApplyCurrentSort(rows, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.ProductRowId)
            .ToListAsync(cancellationToken);

        var pageRowsById = await _dbContext.ParserProductRows
            .AsNoTracking()
            .Where(x => pageProductIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var pageRows = pageProductIds
            .Select(id => pageRowsById.GetValueOrDefault(id))
            .Where(x => x is not null)
            .Cast<ParserProductRow>()
            .ToList();

        var evidence = await LoadListEvidenceAsync(pageRows, ParserRunScope.Production, cancellationToken);
        var items = pageRows
            .Select(row => MapToListItem(row, evidence))
            .ToList();

        return ServiceResult<PagedResponse<ParserProductListItemDto>>.Success(
            new PagedResponse<ParserProductListItemDto>(items, page, pageSize, totalCount));
    }

    private async Task<ServiceResult<ParserProductFilterOptionsDto>> GetCurrentProductFilterOptionsAsync(
        ParserProductFilterOptionsQuery query,
        CancellationToken cancellationToken)
    {
        if (CanUseProductionFilterOptionsCache(query))
        {
            var cached = await GetCachedProductionFilterOptionsAsync(ParserRunScope.Production, cancellationToken);
            if (cached is not null)
                return ServiceResult<ParserProductFilterOptionsDto>.Success(cached);
        }

        var rows = _dbContext.ParserCurrentProductRows.AsNoTracking();
        var searchedRows = ApplyCurrentSearch(rows, query.Search);
        var categoryRows = searchedRows;
        var subcategoryRows = ApplyCurrentFilterOptionValue(searchedRows, nameof(ParserCurrentProductRow.SourceCategory), query.SourceCategory);
        var brandRows = ApplyCurrentFilterOptionValue(subcategoryRows, nameof(ParserCurrentProductRow.SourceSubcategory), query.SourceSubcategory);
        var sellerRows = ApplyCurrentFilterOptionValue(brandRows, nameof(ParserCurrentProductRow.BrandName), query.BrandName);

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

    private async Task<IQueryable<ParserProductRow>?> BuildEffectiveProductRowsAsync(
        string? parserRunId,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        var rows = _dbContext.ParserProductRows.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(parserRunId))
            return rows.Where(x => x.ParserRunId == parserRunId.Trim());

        var productRunIds = await ResolveParserRunIdsAsync(ProductsKind, runScope, cancellationToken);
        if (productRunIds.Count == 0)
            return null;

        var scopedRows = rows.Where(x => productRunIds.Contains(x.ParserRunId));
        var latestRowIds = scopedRows
            .GroupBy(x => x.WbProductId)
            .Select(group => group
                .OrderByDescending(x => x.ParsedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .First());

        return rows.Where(x => latestRowIds.Contains(x.Id));
    }

    private async Task<IReadOnlyList<string>> ResolveParserRunIdsAsync(
        string kind,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x =>
                x.Kind == kind
                && (x.ManifestStatus == SucceededStatus
                    || (kind == ProductsKind && x.ManifestStatus == PartialStatus)))
            .OrderByDescending(x => x.FinishedAtUtc.HasValue)
            .ThenByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.DateRegisteredUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(runScope.Matches)
            .Select(x => x.ParserRunId)
            .ToList();
    }

    private async Task<ParserProductFilterOptionsDto?> GetCachedProductionFilterOptionsAsync(
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        if (!runScope.IsDefaultProduction)
            return null;

        var now = DateTime.UtcNow;
        if (_productionProductFilterOptionsCache is not null
            && _productionProductFilterOptionsCacheExpiresAtUtc > now)
        {
            return _productionProductFilterOptionsCache;
        }

        await ProductFilterOptionsCacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTime.UtcNow;
            if (_productionProductFilterOptionsCache is not null
                && _productionProductFilterOptionsCacheExpiresAtUtc > now)
            {
                return _productionProductFilterOptionsCache;
            }

            var rows = _dbContext.ParserCurrentProductRows.AsNoTracking();
            if (!await rows.AnyAsync(cancellationToken))
            {
                _productionProductFilterOptionsCache = EmptyFilterOptions();
                _productionProductFilterOptionsCacheExpiresAtUtc = now.Add(ProductFilterOptionsCacheDuration);
                return _productionProductFilterOptionsCache;
            }

            var categories = await LoadDistinctOptionValuesAsync(rows.Select(x => x.SourceCategory), cancellationToken);
            var subcategories = await LoadDistinctOptionValuesAsync(rows.Select(x => x.SourceSubcategory), cancellationToken);
            var brands = await LoadDistinctOptionValuesAsync(rows.Select(x => x.BrandName), cancellationToken);
            var sellers = await LoadDistinctOptionValuesAsync(rows.Select(x => x.SellerName), cancellationToken);

            _productionProductFilterOptionsCache = new ParserProductFilterOptionsDto(categories, subcategories, brands, sellers);
            _productionProductFilterOptionsCacheExpiresAtUtc = now.Add(ProductFilterOptionsCacheDuration);
            return _productionProductFilterOptionsCache;
        }
        finally
        {
            ProductFilterOptionsCacheLock.Release();
        }
    }

    private static bool CanUseProductionFilterOptionsCache(ParserProductFilterOptionsQuery query)
    {
        return string.IsNullOrWhiteSpace(query.ParserRunId)
            && !query.IncludeTestRuns
            && !query.TestRunsOnly
            && string.IsNullOrWhiteSpace(query.TestLabel)
            && string.IsNullOrWhiteSpace(query.Search)
            && string.IsNullOrWhiteSpace(query.SourceCategory)
            && string.IsNullOrWhiteSpace(query.SourceSubcategory)
            && string.IsNullOrWhiteSpace(query.BrandName)
            && string.IsNullOrWhiteSpace(query.SellerName);
    }

    private async Task<string?> ResolveEffectiveProductRunIdAsync(
        string? parserRunId,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        return !string.IsNullOrWhiteSpace(parserRunId)
            ? parserRunId.Trim()
            : await ResolveLatestParserRunIdAsync(ProductsKind, runScope, cancellationToken);
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

    private static IQueryable<ParserCurrentProductRow> ApplyCurrentSearch(
        IQueryable<ParserCurrentProductRow> rows,
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

    private static IQueryable<ParserCurrentProductRow> ApplyCurrentFilterOptionValue(
        IQueryable<ParserCurrentProductRow> rows,
        string fieldName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return rows;

        var normalized = value.Trim().ToLowerInvariant();
        return fieldName switch
        {
            nameof(ParserCurrentProductRow.SourceCategory) => rows.Where(x =>
                x.SourceCategory != null && x.SourceCategory.Trim().ToLower() == normalized),
            nameof(ParserCurrentProductRow.SourceSubcategory) => rows.Where(x =>
                x.SourceSubcategory != null && x.SourceSubcategory.Trim().ToLower() == normalized),
            nameof(ParserCurrentProductRow.BrandName) => rows.Where(x =>
                x.BrandName != null && x.BrandName.Trim().ToLower() == normalized),
            nameof(ParserCurrentProductRow.SellerName) => rows.Where(x =>
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

    private async Task<IQueryable<ParserProductRow>> ApplyRequireDeliveryProfileFilterAsync(
        IQueryable<ParserProductRow> rows,
        ParserProductListQuery query,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        if (!query.RequireDeliveryProfile)
            return rows;

        var latestLogisticsRunId = await ResolveLatestParserRunIdAsync(LogisticsKind, runScope, cancellationToken);
        if (latestLogisticsRunId is null)
            return rows.Where(_ => false);

        return rows.Where(product => _dbContext.ParserLogisticsSnapshotRows
            .AsNoTracking()
            .Any(logistics =>
                logistics.ParserRunId == latestLogisticsRunId
                && logistics.WbProductId == product.WbProductId
                && logistics.DeliveryProfileKey != null
                && logistics.DeliveryProfileKey != ""));
    }

    private async Task<string?> ResolveLatestParserRunIdAsync(
        string kind,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.ParserRuns
            .AsNoTracking()
            .Where(x =>
                x.Kind == kind
                && (x.ManifestStatus == SucceededStatus
                    || (kind == ProductsKind && x.ManifestStatus == PartialStatus)))
            .OrderByDescending(x => x.FinishedAtUtc.HasValue)
            .ThenByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.DateRegisteredUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(runScope.Matches)
            .Select(x => x.ParserRunId)
            .FirstOrDefault();
    }

    private async Task<ParserRunScope> LoadScopeForParserRunAsync(
        string parserRunId,
        CancellationToken cancellationToken)
    {
        var run = await _dbContext.ParserRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ParserRunId == parserRunId, cancellationToken);

        if (run is null || !ParserRunScope.IsTestRun(run))
            return ParserRunScope.Production;

        return new ParserRunScope(
            IncludeTestRuns: true,
            TestRunsOnly: true,
            TestLabel: ParserRunScope.TestLabelOf(run));
    }

    private async Task<ParserProductDetailRow?> LoadProductDetailAsync(
        ParserProductRow product,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ParserProductDetailRows
            .AsNoTracking()
            .Where(x =>
                x.WbProductId == product.WbProductId
                && x.Status == "succeeded")
            .OrderByDescending(x => x.InputProductsParserRunId == product.ParserRunId)
            .ThenByDescending(x => x.ParsedAtUtc)
            .ThenByDescending(x => x.SourceLineNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ProductEvidenceLookup> LoadEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        bool includeLogisticsDetail,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
            return ProductEvidenceLookup.Empty;

        var latestRankRunId = await ResolveLatestParserRunIdAsync(RanksKind, runScope, cancellationToken);
        var ranks = latestRankRunId is null
            ? new Dictionary<Guid, ParserProductRankSummaryDto>()
            : await LoadRankSummariesAsync(products, latestRankRunId, cancellationToken);
        var positions = latestRankRunId is null
            ? BuildUnknownPositions(products)
            : await LoadPositionSummariesAsync(products, latestRankRunId, ranks, cancellationToken);
        var reviews = await LoadReviewEvidenceAsync(products, cancellationToken);
        var latestLogisticsRunId = await ResolveLatestParserRunIdAsync(LogisticsKind, runScope, cancellationToken);
        var logistics = latestLogisticsRunId is null
            ? ProductLogisticsEvidence.Empty
            : await LoadLogisticsEvidenceAsync(products, latestLogisticsRunId, includeLogisticsDetail, cancellationToken);
        return new ProductEvidenceLookup(ranks, positions, reviews, logistics.Summaries, logistics.Details, logistics.DeliveryProfiles);
    }

    private async Task<ProductEvidenceLookup> LoadListEvidenceAsync(
        IReadOnlyList<ParserProductRow> products,
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
            return ProductEvidenceLookup.Empty;

        var latestRankRunId = await ResolveLatestParserRunIdAsync(RanksKind, runScope, cancellationToken);
        var ranks = latestRankRunId is null
            ? new Dictionary<Guid, ParserProductRankSummaryDto>()
            : await LoadRankSummariesAsync(products, latestRankRunId, cancellationToken);
        var positions = latestRankRunId is null
            ? BuildUnknownPositions(products)
            : await LoadPositionSummariesAsync(products, latestRankRunId, ranks, cancellationToken);

        return new ProductEvidenceLookup(
            ranks,
            positions,
            new Dictionary<Guid, ParserProductReviewEvidenceDto>(),
            new Dictionary<Guid, ParserProductLogisticsSummaryDto>(),
            new Dictionary<Guid, ParserProductLogisticsDetailDto>(),
            new Dictionary<Guid, ParserProductDeliveryProfileDto>());
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
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryProfileVersion,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationLabel,
                x.DeliveryDestinationAddress,
                x.DeliveryDestinationLatitude,
                x.DeliveryDestinationLongitude,
                x.WbProductId,
                x.TotalQuantityObserved,
                x.QuantityIsCapped,
                x.QuantityCapObserved,
                x.QuantitySemantics,
                x.ProductWhRaw,
                x.ProductTime1Raw,
                x.ProductTime2Raw,
                x.ProductDtypeRaw,
                x.ProductDistRaw,
                x.VisibleDeliveryStatus,
                x.VisibleDeliveryLabel,
                x.VisibleDeliveryDate,
                x.VisibleDeliverySource,
                x.VisibleDeliveryObservedAtUtc,
                x.VisibleDeliveryRawPayload))
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
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryProfileVersion,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationLabel,
                x.DeliveryDestinationAddress,
                x.DeliveryDestinationLatitude,
                x.DeliveryDestinationLongitude,
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
        var deliveryProfiles = includeDetails
            ? new Dictionary<Guid, ParserProductDeliveryProfileDto>()
            : new Dictionary<Guid, ParserProductDeliveryProfileDto>();

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
                    selected.DeliveryProfileKey,
                    selected.DeliveryDestinationName,
                    selected.DeliveryProfileVersion,
                    DeliveryDestinationCity(selected.SourceRegionDest, selected.DeliveryDestinationCity, selected.DeliveryDestinationName),
                    DeliveryDestinationLabel(selected.SourceRegionDest, selected.DeliveryDestinationLabel),
                    DeliveryDestinationAddress(selected.SourceRegionDest, selected.DeliveryDestinationAddress),
                    selected.DeliveryDestinationLatitude,
                    selected.DeliveryDestinationLongitude,
                    selected.ProductTime1Raw,
                    selected.ProductTime2Raw,
                    selected.ProductDtypeRaw,
                    selected.ProductDistRaw,
                    selectedWarehouseRows.Select(MapWarehouseAvailability).ToList());
                deliveryProfiles[product.Id] = MapDeliveryProfile(
                    latestLogisticsRunId,
                    candidates,
                    warehouseByProductAndDestination);
            }
        }

        return new ProductLogisticsEvidence(summaries, details, deliveryProfiles);
    }

    private static ParserProductDeliveryProfileDto MapDeliveryProfile(
        string latestLogisticsRunId,
        IReadOnlyList<LogisticsSnapshotCandidate> candidates,
        IReadOnlyDictionary<LogisticsWarehouseKey, List<WarehouseAvailabilityCandidate>> warehouseByProductAndDestination)
    {
        var latestByDestination = candidates
            .GroupBy(x => x.SourceRegionDest)
            .Select(x => x
                .OrderByDescending(row => row.ObservedAtUtc)
                .ThenByDescending(row => row.SourceLineNumber)
                .First())
            .OrderBy(x => x.DeliveryProfileKey)
            .ThenBy(x => x.SourceRegionDest, StringComparer.Ordinal)
            .ToList();
        var signals = latestByDestination
            .Select(snapshot =>
            {
                var key = new LogisticsWarehouseKey(snapshot.WbProductId, snapshot.SourceRegionDest);
                warehouseByProductAndDestination.TryGetValue(key, out var warehouseRows);
                warehouseRows ??= [];
                var warehouseCount = warehouseRows
                    .Select(x => x.WarehouseIdOnMp)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var visibleDelivery = WbVisibleDeliveryCalculator.Calculate(
                    snapshot.ProductTime1Raw,
                    snapshot.ProductTime2Raw,
                    snapshot.ProductDtypeRaw,
                    snapshot.TotalQuantityObserved,
                    snapshot.ObservedAtUtc);
                return new ParserProductDeliveryDestinationSignalDto(
                    snapshot.SourceRegionDest,
                    snapshot.DeliveryProfileKey,
                    snapshot.DeliveryDestinationName,
                    snapshot.DeliveryProfileVersion,
                    DeliveryDestinationCity(snapshot.SourceRegionDest, snapshot.DeliveryDestinationCity, snapshot.DeliveryDestinationName),
                    DeliveryDestinationLabel(snapshot.SourceRegionDest, snapshot.DeliveryDestinationLabel),
                    DeliveryDestinationAddress(snapshot.SourceRegionDest, snapshot.DeliveryDestinationAddress),
                    snapshot.DeliveryDestinationLatitude,
                    snapshot.DeliveryDestinationLongitude,
                    snapshot.TotalQuantityObserved,
                    warehouseCount,
                    snapshot.ProductTime1Raw,
                    snapshot.ProductTime2Raw,
                    snapshot.ProductDistRaw,
                    visibleDelivery.Status,
                    visibleDelivery.Label,
                    visibleDelivery.Date,
                    visibleDelivery.Source,
                    visibleDelivery.ObservedAtUtc,
                    visibleDelivery.RawPayload,
                    snapshot.ObservedAtUtc);
            })
            .ToList();
        var time1Values = signals
            .Select(x => x.ProductTime1Raw)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        var warehouseSourceCount = signals.Sum(x => x.WarehouseCount);
        var locationEstimate = WbDeliveryLocationEstimator.Estimate(signals
            .Select(x => new WbDeliveryLocationSignal(
                x.DeliveryDestinationName ?? x.DeliveryDestinationCity ?? x.Destination,
                x.VisibleDeliveryLabel,
                DeliveryHoursFrom(x),
                x.TotalQuantityObserved,
                x.VisibleDeliveryStatus))
            .ToList());

        return new ParserProductDeliveryProfileDto(
            latestLogisticsRunId,
            signals.Count,
            warehouseSourceCount,
            time1Values.Count == 0 ? null : time1Values.Min(),
            time1Values.Count == 0 ? null : time1Values.Max(),
            time1Values.Count == 0 ? null : time1Values.Max() - time1Values.Min(),
            MapLocationEstimate(locationEstimate),
            signals);
    }

    private static int? DeliveryHoursFrom(ParserProductDeliveryDestinationSignalDto signal)
    {
        if (signal.VisibleDeliveryRawPayload is { ValueKind: JsonValueKind.Object } rawPayload)
        {
            if (rawPayload.TryGetProperty("deliveryHours", out var camelCaseHours)
                && camelCaseHours.TryGetInt32(out var deliveryHours))
            {
                return deliveryHours;
            }

            if (rawPayload.TryGetProperty("delivery_hours", out var snakeCaseHours)
                && snakeCaseHours.TryGetInt32(out deliveryHours))
            {
                return deliveryHours;
            }
        }

        return signal.ProductTime1Raw.HasValue && signal.ProductTime2Raw.HasValue
            ? signal.ProductTime1Raw.Value + signal.ProductTime2Raw.Value
            : null;
    }

    private static ParserProductDeliveryLocationEstimateDto MapLocationEstimate(WbDeliveryLocationEstimate estimate)
    {
        return new ParserProductDeliveryLocationEstimateDto(
            estimate.Status,
            estimate.ZoneKey,
            estimate.ZoneTitle,
            estimate.Confidence,
            estimate.NearestDestinationName,
            estimate.NearestDeliveryLabel,
            estimate.NearestDeliveryHours,
            estimate.SecondDestinationName,
            estimate.SecondDeliveryHours,
            estimate.FarthestDestinationName,
            estimate.DeliverySpreadHours,
            estimate.Evidence);
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

        if (ReferenceEquals(scopedCandidates, candidates))
        {
            var moscowCandidates = candidates
                .Where(x =>
                    x.DeliveryProfileKey == "moscow_baseline_v1"
                    || x.DeliveryProfileKey == "nationwide_v1" && x.DeliveryDestinationName == "РњРѕСЃРєРІР°")
                .ToList();
            if (moscowCandidates.Count > 0)
                scopedCandidates = moscowCandidates;
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

    private static string QuantityLabel(
        int? totalQuantityObserved,
        bool? quantityIsCapped,
        int? quantityCapObserved)
    {
        if (quantityIsCapped == true && quantityCapObserved.HasValue)
            return $"в‰Ґ{quantityCapObserved.Value.ToString(CultureInfo.InvariantCulture)}";

        return totalQuantityObserved.HasValue
            ? totalQuantityObserved.Value.ToString(CultureInfo.InvariantCulture)
            : "РќРµС‚ РґР°РЅРЅС‹С…";
    }

    private static ParserWarehouseAvailabilityDto MapWarehouseAvailability(WarehouseAvailabilityCandidate row)
    {
        return new ParserWarehouseAvailabilityDto(
            row.WarehouseIdOnMp,
            row.DeliveryProfileKey,
            row.DeliveryDestinationName,
            row.DeliveryProfileVersion,
            DeliveryDestinationCity(row.SourceRegionDest, row.DeliveryDestinationCity, row.DeliveryDestinationName),
            DeliveryDestinationLabel(row.SourceRegionDest, row.DeliveryDestinationLabel),
            DeliveryDestinationAddress(row.SourceRegionDest, row.DeliveryDestinationAddress),
            row.DeliveryDestinationLatitude,
            row.DeliveryDestinationLongitude,
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
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryProfileVersion,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationLabel,
                x.DeliveryDestinationAddress,
                x.DeliveryDestinationLatitude,
                x.DeliveryDestinationLongitude,
                x.WbProductId,
                x.TotalQuantityObserved,
                x.QuantityIsCapped,
                x.QuantityCapObserved,
                x.QuantitySemantics,
                x.ProductWhRaw,
                x.ProductTime1Raw,
                x.ProductTime2Raw,
                x.ProductDtypeRaw,
                x.ProductDistRaw,
                x.VisibleDeliveryStatus,
                x.VisibleDeliveryLabel,
                x.VisibleDeliveryDate,
                x.VisibleDeliverySource,
                x.VisibleDeliveryObservedAtUtc,
                x.VisibleDeliveryRawPayload))
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
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryProfileVersion,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationLabel,
                x.DeliveryDestinationAddress,
                x.DeliveryDestinationLatitude,
                x.DeliveryDestinationLongitude,
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

    private async Task<IReadOnlyList<ParserProductLogisticsDestinationSummaryDto>> LoadLogisticsDestinationSummariesAsync(
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
            .Select(x => new
            {
                x.SourceRegionDest,
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryProfileVersion,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationLabel,
                x.DeliveryDestinationAddress,
                x.DeliveryDestinationLatitude,
                x.DeliveryDestinationLongitude,
                x.WbProductId,
                x.ObservedAtUtc
            })
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(x => new
            {
                x.SourceRegionDest,
                x.DeliveryProfileKey,
                x.DeliveryDestinationName,
                x.DeliveryProfileVersion,
                x.DeliveryDestinationCity,
                x.DeliveryDestinationLabel,
                x.DeliveryDestinationAddress,
                x.DeliveryDestinationLatitude,
                x.DeliveryDestinationLongitude
            })
            .OrderBy(x => x.Key.DeliveryProfileKey)
            .ThenBy(x => x.Key.DeliveryDestinationName)
            .ThenBy(x => x.Key.SourceRegionDest, StringComparer.Ordinal)
            .Select(x => new ParserProductLogisticsDestinationSummaryDto(
                x.Key.SourceRegionDest,
                x.Key.DeliveryProfileKey,
                x.Key.DeliveryDestinationName,
                x.Key.DeliveryProfileVersion,
                DeliveryDestinationCity(x.Key.SourceRegionDest, x.Key.DeliveryDestinationCity, x.Key.DeliveryDestinationName),
                DeliveryDestinationLabel(x.Key.SourceRegionDest, x.Key.DeliveryDestinationLabel),
                DeliveryDestinationAddress(x.Key.SourceRegionDest, x.Key.DeliveryDestinationAddress),
                x.Key.DeliveryDestinationLatitude,
                x.Key.DeliveryDestinationLongitude,
                x.Select(row => row.WbProductId).Distinct(StringComparer.Ordinal).Count(),
                x.Max(row => row.ObservedAtUtc)))
            .ToList();
    }

    private static ParserProductLogisticsSummaryAggregateDto BuildLogisticsAggregate(
        string? productRunId,
        string? logisticsRunId,
        ParserProductLogisticsSummaryQuery query,
        int productsTotal,
        IReadOnlyList<SelectedLogisticsObservation> observations,
        IReadOnlyList<ParserProductLogisticsDestinationSummaryDto> destinationSummaries)
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
        var destinations = destinationSummaries;
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

    private static bool CanUseCurrentProductRows(ParserProductListQuery query, ParserRunScope runScope)
    {
        return runScope.IsDefaultProduction
            && string.IsNullOrWhiteSpace(query.ParserRunId)
            && !query.RequireDeliveryProfile;
    }

    private static bool CanUseCurrentProductFilterOptions(ParserProductFilterOptionsQuery query, ParserRunScope runScope)
    {
        return runScope.IsDefaultProduction
            && string.IsNullOrWhiteSpace(query.ParserRunId);
    }

    private static IQueryable<ParserCurrentProductRow> ApplyCurrentFilters(
        IQueryable<ParserCurrentProductRow> rows,
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

        return ApplyCurrentSearch(rows, query.Search);
    }

    private static IOrderedQueryable<ParserCurrentProductRow> ApplyCurrentSort(
        IQueryable<ParserCurrentProductRow> rows,
        string? sort)
    {
        return sort?.Trim() switch
        {
            "name" => rows.OrderBy(x => x.Name).ThenBy(x => x.ProductRowId),
            "-name" => rows.OrderByDescending(x => x.Name).ThenByDescending(x => x.ProductRowId),
            "parsedAtUtc" => rows.OrderBy(x => x.ParsedAtUtc).ThenBy(x => x.ProductRowId),
            "wbProductId" => rows.OrderBy(x => x.WbProductId).ThenBy(x => x.ProductRowId),
            "-wbProductId" => rows.OrderByDescending(x => x.WbProductId).ThenByDescending(x => x.ProductRowId),
            "position" => rows
                .OrderBy(x =>
                    x.PositionState == PositionStateObserved
                        ? 0
                        : x.PositionState == PositionStateBeyondObservedRange
                            ? 1
                            : 2)
                .ThenBy(x => x.PositionAbsolute ?? x.PositionObservedRangeLimit ?? int.MaxValue)
                .ThenBy(x => x.ProductRowId),
            "-position" => rows
                .OrderBy(x =>
                    x.PositionState == PositionStateObserved
                        ? 2
                        : x.PositionState == PositionStateBeyondObservedRange
                            ? 1
                            : 0)
                .ThenByDescending(x => x.PositionAbsolute ?? x.PositionObservedRangeLimit ?? 0)
                .ThenByDescending(x => x.ProductRowId),
            "price" or "priceDiscounted" => rows.OrderBy(x => x.PriceDiscounted == null).ThenBy(x => x.PriceDiscounted).ThenBy(x => x.ProductRowId),
            "-price" or "-priceDiscounted" => rows.OrderBy(x => x.PriceDiscounted == null).ThenByDescending(x => x.PriceDiscounted).ThenByDescending(x => x.ProductRowId),
            "reviewRating" => rows.OrderBy(x => x.ReviewRating).ThenBy(x => x.ProductRowId),
            "-reviewRating" => rows.OrderByDescending(x => x.ReviewRating).ThenByDescending(x => x.ProductRowId),
            "feedbackCount" => rows.OrderBy(x => x.FeedbackCount).ThenBy(x => x.ProductRowId),
            "-feedbackCount" => rows.OrderByDescending(x => x.FeedbackCount).ThenByDescending(x => x.ProductRowId),
            _ => rows.OrderByDescending(x => x.ParsedAtUtc).ThenByDescending(x => x.ProductRowId)
        };
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
        ParserRunScope runScope,
        CancellationToken cancellationToken)
    {
        var totalCount = await rows.CountAsync(cancellationToken);
        if (totalCount == 0)
            return (0, []);

        var allRows = await rows.ToListAsync(cancellationToken);
        var latestRankRunId = await ResolveLatestParserRunIdAsync(RanksKind, runScope, cancellationToken);
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
            evidence.GetDeliveryProfile(row.Id),
            Description: details?.Description,
            Characteristics: details?.Characteristics?.RootElement.Clone(),
            GroupedOptions: details?.GroupedOptions?.RootElement.Clone(),
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

    private static IEnumerable<string> ExtractCharacteristicNames(JsonDocument? characteristics)
    {
        if (characteristics is null || characteristics.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return characteristics.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("name", out _))
            .Select(x => x.GetProperty("name").GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }
}
