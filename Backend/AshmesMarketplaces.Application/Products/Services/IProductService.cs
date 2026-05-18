using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Products.Dtos;

namespace AshmesMarketplaces.Application.Products.Services;

public interface IProductService
{
    Task<ServiceResult<PagedResponse<ProductListItemResponse>>> GetListAsync(
        ProductListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProductResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProductResponse>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProductResponse>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
