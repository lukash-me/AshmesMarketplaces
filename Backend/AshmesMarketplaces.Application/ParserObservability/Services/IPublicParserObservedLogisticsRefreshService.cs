using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IPublicParserObservedLogisticsRefreshService
{
    Task<ServiceResult<PublicParserObservedLogisticsRefreshResult>> RefreshAsync(CancellationToken cancellationToken);
}

public sealed record PublicParserObservedLogisticsRefreshResult(
    int MarketEventsCount,
    int StockDecreasesCount,
    DateTime CalculatedAtUtc);
