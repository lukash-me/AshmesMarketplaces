using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public sealed class PublicMarketIntelligenceSnapshotReadService : IPublicMarketIntelligenceSnapshotReadService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public PublicMarketIntelligenceSnapshotReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PublicMarketIntelligenceDto>> GetAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        var context = PublicMarketIntelligenceContextCatalog.Resolve(query);
        if (context is null)
            return ServiceResult<PublicMarketIntelligenceDto>.BadRequest("Ниша маркетинговой разведки не поддерживается.");

        var snapshot = await _dbContext.PublicMarketIntelligenceSnapshots
            .AsNoTracking()
            .Where(x =>
                x.SourceCategory == context.SourceCategory
                && x.SourceSubcategory == context.SourceSubcategory
                && x.Query == context.Query
                && x.SourceRegionDest == context.SourceRegionDest
                && x.Sort == context.Sort
                && x.TopN == context.TopN
                && x.CalculatedAtUtc != null
                && x.Status == PublicMarketIntelligenceSnapshot.CompletedStatus)
            .OrderByDescending(x => x.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            snapshot = await _dbContext.PublicMarketIntelligenceSnapshots
                .AsNoTracking()
                .Where(x =>
                    x.SourceCategory == context.SourceCategory
                    && x.SourceSubcategory == context.SourceSubcategory
                    && x.SourceRegionDest == context.SourceRegionDest
                    && x.Sort == context.Sort
                    && x.TopN == context.TopN
                    && x.CalculatedAtUtc != null
                    && x.Status == PublicMarketIntelligenceSnapshot.CompletedStatus)
                .OrderByDescending(x => x.CalculatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (snapshot is null)
            return ServiceResult<PublicMarketIntelligenceDto>.NotFound("Маркетинговая разведка еще не рассчитана.");

        var dto = JsonSerializer.Deserialize<PublicMarketIntelligenceDto>(
            snapshot.PublicMarketIntelligenceJson,
            JsonOptions);

        return dto is null
            ? ServiceResult<PublicMarketIntelligenceDto>.Unavailable("Сохраненный расчет маркетинговой разведки поврежден.")
            : ServiceResult<PublicMarketIntelligenceDto>.Success(dto);
    }

    public async Task<IReadOnlyList<PublicMarketIntelligenceContextAvailabilityDto>> GetAvailableContextsAsync(
        CancellationToken cancellationToken)
    {
        var snapshots = await _dbContext.PublicMarketIntelligenceSnapshots
            .AsNoTracking()
            .Where(x =>
                x.Status == PublicMarketIntelligenceSnapshot.CompletedStatus
                && x.CalculatedAtUtc != null
                && x.SampleSize > 0)
            .OrderBy(x => x.SourceSubcategory)
            .ThenByDescending(x => x.CalculatedAtUtc)
            .Select(x => new
            {
                x.SourceCategory,
                x.SourceSubcategory,
                x.Query,
                x.SourceRegionDest,
                x.Sort,
                x.TopN,
                x.SampleSize,
                x.CalculatedAtUtc,
                x.LatestObservedAtUtc
            })
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(x => new
            {
                x.SourceCategory,
                x.SourceSubcategory,
                x.SourceRegionDest,
                x.Sort,
                x.TopN
            })
            .Select(group => group.First())
            .OrderBy(x => x.SourceSubcategory, StringComparer.Ordinal)
            .Select(x => new PublicMarketIntelligenceContextAvailabilityDto(
                x.SourceCategory,
                x.SourceSubcategory,
                x.Query,
                x.SourceRegionDest,
                x.Sort,
                x.TopN,
                x.SampleSize,
                x.CalculatedAtUtc,
                x.LatestObservedAtUtc))
            .ToList();
    }
}
