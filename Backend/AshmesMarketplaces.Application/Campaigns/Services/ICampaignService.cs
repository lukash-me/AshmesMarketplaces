using AshmesMarketplaces.Application.Campaigns.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.Campaigns.Services;

public interface ICampaignService
{
    Task<ServiceResult<PagedResponse<CampaignListItemResponse>>> GetListAsync(CampaignListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<CampaignResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<CampaignResponse>> CreateAsync(CreateCampaignRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CampaignResponse>> UpdateAsync(Guid id, UpdateCampaignRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
