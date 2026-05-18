using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RecommendationCategories.Dtos;

namespace AshmesMarketplaces.Application.RecommendationCategories.Services;

public interface IRecommendationCategoryService
{
    Task<ServiceResult<PagedResponse<RecommendationCategoryListItemResponse>>> GetListAsync(RecommendationCategoryListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationCategoryResponse>> GetByIdAsync(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationCategoryResponse>> CreateAsync(CreateRecommendationCategoryRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken);
}
