using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IPublicProductAvailabilityReadService
{
    Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetAsync(
        ParserProductListQuery query,
        CancellationToken cancellationToken);
}
