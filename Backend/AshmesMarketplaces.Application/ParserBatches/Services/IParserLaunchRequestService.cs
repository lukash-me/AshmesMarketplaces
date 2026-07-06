using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserLaunchRequestService
{
    Task<ServiceResult<ParserLaunchRequestDto>> RequestLaunchAsync(
        Guid parserInstanceConfigurationId,
        CreateParserLaunchRequest request,
        CancellationToken cancellationToken);

    Task<ParserLaunchRequest?> ClaimNextAsync(CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken);
}
