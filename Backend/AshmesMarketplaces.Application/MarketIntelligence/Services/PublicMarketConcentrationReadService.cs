using System.Collections.Concurrent;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicMarketConcentrationReadService : IPublicMarketConcentrationReadService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public PublicMarketConcentrationReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicMarketConcentrationSnapshotDto>> GetAsync(
        PublicMarketIntelligenceQuery query,
        bool includePoints,
        CancellationToken cancellationToken)
    {
        var context = PublicMarketIntelligenceContextCatalog.Resolve(query);
        if (context is null)
            return ServiceResult<PublicMarketConcentrationSnapshotDto>.BadRequest("Ниша концентрации рынка не поддерживается.");

        var cacheKey = BuildCacheKey(context, includePoints);
        var now = DateTime.UtcNow;

        var snapshotMeta = await GetSnapshotMetaAsync(context, cancellationToken);
        if (snapshotMeta is not null
            && Cache.TryGetValue(cacheKey, out var cached)
            && cached.UpdatedAtUtc == snapshotMeta.UpdatedAtUtc
            && now - cached.CachedAtUtc <= CacheTtl)
        {
            return ServiceResult<PublicMarketConcentrationSnapshotDto>.Success(cached.Value);
        }

        if (snapshotMeta is null)
            return ServiceResult<PublicMarketConcentrationSnapshotDto>.NotFound("Концентрация рынка еще не рассчитана.");

        var snapshot = await _dbContext.PublicMarketConcentrationSnapshots
            .AsNoTracking()
            .FirstAsync(x => x.Id == snapshotMeta.Id, cancellationToken);

        var dto = PublicMarketConcentrationRefreshService.Map(snapshot, includePoints);
        Cache[cacheKey] = new CacheEntry(dto, snapshot.UpdatedAtUtc, now);

        return ServiceResult<PublicMarketConcentrationSnapshotDto>.Success(dto);
    }

    public async Task<ServiceResult<PublicMarketConcentrationProductsDto>> GetProductsAsync(
        PublicMarketIntelligenceQuery query,
        string kind,
        string key,
        CancellationToken cancellationToken)
    {
        var context = PublicMarketIntelligenceContextCatalog.Resolve(query);
        if (context is null)
            return ServiceResult<PublicMarketConcentrationProductsDto>.BadRequest("Ниша концентрации рынка не поддерживается.");

        var normalizedKind = kind.Trim().ToLowerInvariant();
        if (normalizedKind is not ("seller" or "brand" or "root"))
            return ServiceResult<PublicMarketConcentrationProductsDto>.BadRequest("Неподдерживаемый разрез карточек концентрации.");

        var snapshotMeta = await GetSnapshotMetaAsync(context, cancellationToken);
        if (snapshotMeta is null)
            return ServiceResult<PublicMarketConcentrationProductsDto>.NotFound("Концентрация рынка еще не рассчитана.");

        var snapshot = await _dbContext.PublicMarketConcentrationSnapshots
            .AsNoTracking()
            .FirstAsync(x => x.Id == snapshotMeta.Id, cancellationToken);

        var products = JsonSerializer.Deserialize<List<PriceQualityPointDto>>(snapshot.PriceQualityPointsJson, JsonOptions) ?? [];
        var filtered = products
            .Where(point => ProductMatches(point, normalizedKind, key))
            .ToList();

        return ServiceResult<PublicMarketConcentrationProductsDto>.Success(
            new PublicMarketConcentrationProductsDto(
                new PublicMarketContextDto(
                    "wildberries",
                    snapshot.SourceCategory,
                    snapshot.SourceSubcategory,
                    snapshot.Query,
                    snapshot.SourceRegionDest,
                    snapshot.Sort,
                    snapshot.TopN),
                normalizedKind,
                key,
                filtered));
    }

    private async Task<SnapshotMeta?> GetSnapshotMetaAsync(
        PublicMarketIntelligenceQuery context,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PublicMarketConcentrationSnapshots
            .AsNoTracking()
            .Where(x =>
                x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && x.Sort == context.Sort
                && x.TopN == context.TopN
                && x.CalculatedAtUtc != null)
            .Select(x => new SnapshotMeta(x.Id, x.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool ProductMatches(PriceQualityPointDto point, string kind, string key)
    {
        return kind switch
        {
            "seller" => string.Equals(point.SellerName, key, StringComparison.Ordinal),
            "brand" => string.Equals(point.BrandName, key, StringComparison.Ordinal),
            "root" => string.Equals(point.WbRootId, key, StringComparison.Ordinal),
            _ => false
        };
    }

    private static string BuildCacheKey(PublicMarketIntelligenceQuery context, bool includePoints)
    {
        return string.Join(
            '\u001f',
            includePoints ? "with-points" : "summary",
            context.SourceCategory,
            context.SourceSubcategory,
            context.Query,
            context.SourceRegionDest,
            context.Sort,
            context.TopN.ToString());
    }

    private sealed record SnapshotMeta(Guid Id, DateTime UpdatedAtUtc);

    private sealed record CacheEntry(
        PublicMarketConcentrationSnapshotDto Value,
        DateTime UpdatedAtUtc,
        DateTime CachedAtUtc);
}
