using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserBatchQueueService
{
    Task<ServiceResult<ParserBatchSubmitResponse>> SubmitAsync(
        ParserBatchSubmitRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserBatchStatusResponse>> GetStatusAsync(
        string externalBatchId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserPendingAckResponse>> GetPendingAcksAsync(
        string parserInstanceId,
        CancellationToken cancellationToken);
}
