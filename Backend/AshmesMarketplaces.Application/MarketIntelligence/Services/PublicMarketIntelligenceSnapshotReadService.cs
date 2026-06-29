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
            return ServiceResult<PublicMarketIntelligenceDto>.NotFound("Маркетинговая разведка еще не рассчитана.");

        var dto = JsonSerializer.Deserialize<PublicMarketIntelligenceDto>(
            snapshot.PublicMarketIntelligenceJson,
            JsonOptions);

        return dto is null
            ? ServiceResult<PublicMarketIntelligenceDto>.Unavailable("Сохраненный расчет маркетинговой разведки поврежден.")
            : ServiceResult<PublicMarketIntelligenceDto>.Success(dto);
    }
}
