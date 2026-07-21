using AshmesMarketplaces.Application.Brands.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.Brands.Services;

public interface IBrandService
{
    Task<ServiceResult<PagedResponse<BrandListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<BrandResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<BrandResponse>> CreateAsync(
        CreateBrandRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<BrandResponse>> UpdateAsync(
        Guid id,
        UpdateBrandRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
