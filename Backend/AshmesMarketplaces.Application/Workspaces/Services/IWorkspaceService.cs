using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Workspaces.Dtos;

namespace AshmesMarketplaces.Application.Workspaces.Services;

public interface IWorkspaceService
{
    Task<ServiceResult<PagedResponse<WorkspaceListItemResponse>>> GetListAsync(WorkspaceListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<WorkspaceResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<WorkspaceResponse>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<WorkspaceResponse>> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
