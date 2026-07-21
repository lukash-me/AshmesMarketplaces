using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Roles.Dtos;

namespace AshmesMarketplaces.Application.Roles.Services;

public interface IRoleService
{
    Task<ServiceResult<PagedResponse<RoleListItemResponse>>> GetListAsync(RoleListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RoleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<RoleResponse>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<RoleResponse>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
