using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.PermissionCategories.Dtos;

namespace AshmesMarketplaces.Application.PermissionCategories.Services;

public interface IPermissionCategoryService
{
    Task<ServiceResult<PagedResponse<PermissionCategoryListItemResponse>>> GetListAsync(PermissionCategoryListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<PermissionCategoryResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<PermissionCategoryResponse>> CreateAsync(CreatePermissionCategoryRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PermissionCategoryResponse>> UpdateAsync(Guid id, UpdatePermissionCategoryRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
