using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IParserObservedMarketEventReadService
{
    Task<ServiceResult<ParserObservedMarketEventResponse>> GetListAsync(
        ParserObservedMarketEventQuery query,
        CancellationToken cancellationToken);
}
