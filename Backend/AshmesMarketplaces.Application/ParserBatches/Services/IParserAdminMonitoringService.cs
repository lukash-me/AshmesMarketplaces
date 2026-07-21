using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserAdminMonitoringService
{
    Task<ServiceResult<IReadOnlyList<ParserAdminInstanceDto>>> GetInstancesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ParserAdminProxyRunJournalDto>>> GetJournalAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ParserAdminBatchListItemDto>>> GetBatchesAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserAdminBatchDetailDto>> GetBatchAsync(Guid id, CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ParserAdminNicheDto>>> GetNichesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ParserAdminErrorDto>>> GetErrorsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserAdminRetryResponse>> RetryBatchAsync(Guid id, CancellationToken cancellationToken);
}
