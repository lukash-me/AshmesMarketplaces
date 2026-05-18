using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ReviewReplies.Dtos;

namespace AshmesMarketplaces.Application.ReviewReplies.Services;

public interface IReviewReplyService
{
    Task<ServiceResult<PagedResponse<ReviewReplyListItemResponse>>> GetListAsync(ReviewReplyListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<ReviewReplyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<ReviewReplyResponse>> CreateAsync(CreateReviewReplyRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ReviewReplyResponse>> UpdateAsync(Guid id, UpdateReviewReplyRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
