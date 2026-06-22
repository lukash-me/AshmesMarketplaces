using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicTopForecastRefreshService
{
    Task<ServiceResult<RecalculateTopForecastResponse>> RefreshAsync(CancellationToken cancellationToken);
}
