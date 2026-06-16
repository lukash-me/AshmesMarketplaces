using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public interface IMarketHotProductsRecalculationService
{
    Task<ServiceResult<RecalculateHotProductsResponse>> RecalculateAsync(
        RecalculateHotProductsRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<RecalculateHotProductsResponse>> RecalculateForUserAsync(
        Guid userId,
        RecalculateHotProductsRequest request,
        CancellationToken cancellationToken);
}
