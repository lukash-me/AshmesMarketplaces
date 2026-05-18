using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RolePermissions.Dtos;

namespace AshmesMarketplaces.Application.RolePermissions.Services;

public interface IRolePermissionService
{
    Task<ServiceResult<PagedResponse<RolePermissionListItemResponse>>> GetListAsync(RolePermissionListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RolePermissionResponse>> GetByIdAsync(Guid idRole, Guid idPermission, CancellationToken cancellationToken);
    Task<ServiceResult<RolePermissionResponse>> CreateAsync(CreateRolePermissionRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid idRole, Guid idPermission, CancellationToken cancellationToken);
}
