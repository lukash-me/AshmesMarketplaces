using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Options;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public interface IMarketHotProductsSnapshotBuilder
{
    Task<ServiceResult<MarketHotProductsSnapshot>> BuildAsync(
        RecalculateHotProductsRequest request,
        IntelligenceOptions options,
        CancellationToken cancellationToken);
}
