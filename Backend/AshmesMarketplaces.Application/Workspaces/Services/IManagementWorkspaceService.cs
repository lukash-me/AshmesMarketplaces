using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Workspaces.Dtos;

namespace AshmesMarketplaces.Application.Workspaces.Services;

public interface IManagementWorkspaceService
{
    Task<ServiceResult<IReadOnlyCollection<ManagementWorkspaceResponse>>> GetListAsync(CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyCollection<ManagementWorkspaceRoleResponse>>> GetRolesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<ManagementWorkspaceResponse>> CreateAsync(
        CreateManagementWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ManagementWorkspaceResponse>> UpdateAsync(
        Guid workspaceId,
        UpdateManagementWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ManagementWorkspaceMemberResponse>> AddMemberAsync(
        Guid workspaceId,
        AddManagementWorkspaceMemberRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ManagementWorkspaceMemberResponse>> UpdateMemberRoleAsync(
        Guid workspaceId,
        Guid userId,
        UpdateManagementWorkspaceMemberRoleRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}
