using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Products.Dtos;

namespace AshmesMarketplaces.Application.Products.Services;

public interface IProductHistoryService
{
    Task<ServiceResult<PagedResponse<ProductHistoryListItemResponse>>> GetListAsync(
        Guid productId,
        ProductHistoryListQuery query,
        CancellationToken cancellationToken);
}
