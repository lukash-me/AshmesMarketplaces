using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.UserWorkspaces.Dtos;

namespace AshmesMarketplaces.Application.UserWorkspaces.Services;

public interface IUserWorkspaceService
{
    Task<ServiceResult<PagedResponse<UserWorkspaceListItemResponse>>> GetListAsync(UserWorkspaceListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<UserWorkspaceResponse>> GetByIdAsync(Guid idUser, Guid idWorkspace, CancellationToken cancellationToken);
    Task<ServiceResult<UserWorkspaceResponse>> CreateAsync(CreateUserWorkspaceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<UserWorkspaceResponse>> UpdateAsync(Guid idUser, Guid idWorkspace, UpdateUserWorkspaceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid idUser, Guid idWorkspace, CancellationToken cancellationToken);
}
