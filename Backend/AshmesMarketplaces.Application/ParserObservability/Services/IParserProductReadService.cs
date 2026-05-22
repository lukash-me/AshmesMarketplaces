using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IParserProductReadService
{
    Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetListAsync(
        ParserProductListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProductDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
