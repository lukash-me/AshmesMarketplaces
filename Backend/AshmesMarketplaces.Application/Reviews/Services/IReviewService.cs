using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Reviews.Dtos;

namespace AshmesMarketplaces.Application.Reviews.Services;

public interface IReviewService
{
    Task<ServiceResult<PagedResponse<ReviewListItemResponse>>> GetListAsync(ReviewListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<ReviewResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<ReviewResponse>> CreateAsync(CreateReviewRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ReviewResponse>> UpdateAsync(Guid id, UpdateReviewRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
