using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserProxyRunService
{
    Task<ServiceResult<ParserProxyRunResponse>> StartAsync(
        ParserProxyRunStartRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProxyRunResponse>> UpdateProgressAsync(
        string externalProxyRunId,
        ParserProxyRunProgressRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProxyRunResponse>> FinishAsync(
        string externalProxyRunId,
        ParserProxyRunFinishRequest request,
        CancellationToken cancellationToken);
}
