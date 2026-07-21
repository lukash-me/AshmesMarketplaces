using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public interface IMarketHotProductsReadService
{
    Task<ServiceResult<HotProductsListResponse>> GetHotProductsAsync(
        HotProductsListQuery query,
        CancellationToken cancellationToken);
}
