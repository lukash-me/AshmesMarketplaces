using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicMarketIntelligenceRefreshService
{
    Task<ServiceResult<IReadOnlyList<PublicMarketIntelligenceDto>>> RefreshAllAsync(CancellationToken cancellationToken);

    Task<ServiceResult<PublicMarketIntelligenceDto>> RefreshAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken);
}
