using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Permissions.Dtos;

namespace AshmesMarketplaces.Application.Permissions.Services;

public interface IPermissionService
{
    Task<ServiceResult<PagedResponse<PermissionListItemResponse>>> GetListAsync(PermissionListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<PermissionResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<PermissionResponse>> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PermissionResponse>> UpdateAsync(Guid id, UpdatePermissionRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
