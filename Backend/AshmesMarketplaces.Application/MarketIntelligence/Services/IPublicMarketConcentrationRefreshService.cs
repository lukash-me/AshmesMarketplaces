using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;

namespace AshmesMarketplaces.Application.MarketIntelligence.Services;

public interface IPublicMarketConcentrationRefreshService
{
    Task<ServiceResult<IReadOnlyList<PublicMarketConcentrationSnapshotDto>>> RefreshAllAsync(CancellationToken cancellationToken);

    Task<ServiceResult<PublicMarketConcentrationSnapshotDto>> RefreshAsync(
        PublicMarketIntelligenceQuery query,
        CancellationToken cancellationToken);
}
