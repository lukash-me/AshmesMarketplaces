using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IParserRunReadService
{
    Task<ServiceResult<PagedResponse<ParserRunSummaryDto>>> GetListAsync(
        ParserRunListQuery query,
        CancellationToken cancellationToken);
}
