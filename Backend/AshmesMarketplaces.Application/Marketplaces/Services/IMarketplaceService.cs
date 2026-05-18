using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Marketplaces.Dtos;

namespace AshmesMarketplaces.Application.Marketplaces.Services;

public interface IMarketplaceService
{
    Task<ServiceResult<PagedResponse<MarketplaceListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<MarketplaceResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<MarketplaceResponse>> CreateAsync(
        CreateMarketplaceRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<MarketplaceResponse>> UpdateAsync(
        Guid id,
        UpdateMarketplaceRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
