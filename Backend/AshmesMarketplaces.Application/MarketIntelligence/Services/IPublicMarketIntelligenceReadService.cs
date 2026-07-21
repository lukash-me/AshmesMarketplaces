using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicMarketIntelligenceReadService
{
    Task<ServiceResult<PublicMarketIntelligenceDto>> GetAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken);
}
