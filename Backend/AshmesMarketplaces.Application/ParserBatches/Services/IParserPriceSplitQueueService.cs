using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserPriceSplitQueueService
{
    Task<ServiceResult<ParserPriceSplitJobResponse>> EnsureJobAsync(
        ParserPriceSplitEnsureJobRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserPriceSplitJobResponse>> GetCurrentJobAsync(
        ParserPriceSplitCurrentJobRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserPriceSplitClaimRangesResponse>> ClaimRangesAsync(
        ParserPriceSplitClaimRangesRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserPriceSplitRangeResponse>> UpdateRangeAsync(
        Guid rangeId,
        ParserPriceSplitUpdateRangeRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserPriceSplitRangeResponse>> SplitRangeAsync(
        Guid rangeId,
        ParserPriceSplitSplitRangeRequest request,
        CancellationToken cancellationToken);
}
