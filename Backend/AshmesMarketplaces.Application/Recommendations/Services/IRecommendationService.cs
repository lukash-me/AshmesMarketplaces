using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Recommendations.Dtos;

namespace AshmesMarketplaces.Application.Recommendations.Services;

public interface IRecommendationService
{
    Task<ServiceResult<PagedResponse<RecommendationListItemResponse>>> GetListAsync(RecommendationListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationResponse>> CreateAsync(CreateRecommendationRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationResponse>> UpdateAsync(Guid id, UpdateRecommendationRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
