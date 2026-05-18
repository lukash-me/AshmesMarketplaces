using AshmesMarketplaces.Application.Categories.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.Categories.Services;

public interface ICategoryService
{
    Task<ServiceResult<PagedResponse<CategoryListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<CategoryResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<CategoryResponse>> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<CategoryResponse>> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
