using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IPublicProductAvailabilityRefreshService
{
    Task<ServiceResult<PublicProductAvailabilityRefreshResult>> RefreshAsync(CancellationToken cancellationToken);
}

public sealed record PublicProductAvailabilityRefreshResult(int TotalCount, DateTime CalculatedAtUtc);
