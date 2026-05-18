using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RoleSubroles.Dtos;

namespace AshmesMarketplaces.Application.RoleSubroles.Services;

public interface IRoleSubroleService
{
    Task<ServiceResult<PagedResponse<RoleSubroleListItemResponse>>> GetListAsync(RoleSubroleListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RoleSubroleResponse>> GetByIdAsync(Guid idRole, Guid idSubrole, CancellationToken cancellationToken);
    Task<ServiceResult<RoleSubroleResponse>> CreateAsync(CreateRoleSubroleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid idRole, Guid idSubrole, CancellationToken cancellationToken);
}
