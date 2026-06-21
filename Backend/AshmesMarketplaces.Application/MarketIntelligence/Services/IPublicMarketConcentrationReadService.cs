using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicMarketConcentrationReadService
{
    Task<ServiceResult<PublicMarketConcentrationSnapshotDto>> GetAsync(
        PublicMarketIntelligenceQuery query,
        bool includePoints,
        CancellationToken cancellationToken);

    Task<ServiceResult<PublicMarketConcentrationProductsDto>> GetProductsAsync(
        PublicMarketIntelligenceQuery query,
        string kind,
        string key,
        CancellationToken cancellationToken);
}
