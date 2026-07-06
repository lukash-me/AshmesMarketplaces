using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.MarketplaceCategories.Services;

public interface IWildberriesCategoryCatalogRefreshService
{
    Task<ServiceResult<WildberriesCategoryCatalogRefreshResult>> RefreshAsync(CancellationToken cancellationToken);
}

public sealed record WildberriesCategoryCatalogRefreshResult(
    int LeavesCount,
    DateTime FetchedAtUtc);
