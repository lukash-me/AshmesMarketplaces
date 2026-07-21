using AshmesMarketplaces.Application.CampaignMetrics.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.CampaignMetrics.Services;

public interface ICampaignMetricService
{
    Task<ServiceResult<PagedResponse<CampaignMetricListItemResponse>>> GetListAsync(CampaignMetricListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<CampaignMetricResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<CampaignMetricResponse>> CreateAsync(CreateCampaignMetricRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CampaignMetricResponse>> UpdateAsync(Guid id, UpdateCampaignMetricRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
