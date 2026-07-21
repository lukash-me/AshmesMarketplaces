using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IParserReviewReadService
{
    Task<ServiceResult<PagedResponse<ParserReviewListItemDto>>> GetListAsync(
        ParserReviewListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserReviewDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<PagedResponse<ParserReviewReplyDto>>> GetRepliesAsync(
        Guid id,
        ParserReviewReplyListQuery query,
        CancellationToken cancellationToken);
}
