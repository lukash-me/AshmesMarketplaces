using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicTopForecastReadService
{
    Task<ServiceResult<PublicTopForecastResponse>> GetAsync(
        PublicTopForecastQuery query,
        CancellationToken cancellationToken);
}
