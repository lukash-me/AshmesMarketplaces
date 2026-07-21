using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RecommendationProducts.Dtos;

namespace AshmesMarketplaces.Application.RecommendationProducts.Services;

public interface IRecommendationProductService
{
    Task<ServiceResult<PagedResponse<RecommendationProductListItemResponse>>> GetListAsync(RecommendationProductListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationProductResponse>> GetByIdAsync(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken);
    Task<ServiceResult<RecommendationProductResponse>> CreateAsync(CreateRecommendationProductRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken);
}
