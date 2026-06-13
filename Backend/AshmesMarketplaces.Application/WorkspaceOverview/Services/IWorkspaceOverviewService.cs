using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.WorkspaceOverview.Dtos;

namespace AshmesMarketplaces.Application.WorkspaceOverview.Services;

public interface IWorkspaceOverviewService
{
    Task<ServiceResult<WorkspaceOverviewResponse>> GetAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceOverviewRecalculateResponse>> RecalculateAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceOverviewMarkViewedResponse>> MarkViewedAsync(
        Guid workspaceId,
        WorkspaceOverviewMarkViewedRequest request,
        CancellationToken cancellationToken);
}
