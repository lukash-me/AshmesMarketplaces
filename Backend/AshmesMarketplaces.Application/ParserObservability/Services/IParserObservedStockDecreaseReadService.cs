using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IParserObservedStockDecreaseReadService
{
    Task<ServiceResult<ParserObservedStockDecreaseResponse>> GetListAsync(
        ParserObservedStockDecreaseQuery query,
        CancellationToken cancellationToken);
}
