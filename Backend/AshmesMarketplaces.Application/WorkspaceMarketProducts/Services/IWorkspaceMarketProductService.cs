using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;

namespace AshmesMarketplaces.Application.WorkspaceMarketProducts.Services;

public interface IWorkspaceMarketProductService
{
    Task<ServiceResult<PagedResponse<WorkspaceMarketProductListItemResponse>>> GetListAsync(
        Guid workspaceId,
        WorkspaceMarketProductListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceMarketProductResponse>> GetByIdAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceMarketProductResponse>> AddAsync(
        Guid workspaceId,
        CreateWorkspaceMarketProductRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceMarketProductResponse>> AddDemoAsync(
        Guid workspaceId,
        CreateDemoWorkspaceMarketProductRequest request,
        IReadOnlyList<WorkspaceMarketProductUpload> media,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceMarketProductResponse>> UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateWorkspaceMarketProductRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceMarketProductHistoryResponse>> GetHistoryAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken);
}
