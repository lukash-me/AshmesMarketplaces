using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketplaceCategories.Dtos;

namespace AshmesMarketplaces.Application.MarketplaceCategories.Services;

public interface IWildberriesCategoryCatalogService
{
    Task<ServiceResult<WildberriesCategoryCatalogDto>> GetTreeAsync(CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> GetLeavesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<WildberriesCategoryNodeDto>>> SearchAsync(
        WildberriesCategorySearchQuery query,
        CancellationToken cancellationToken);
}
