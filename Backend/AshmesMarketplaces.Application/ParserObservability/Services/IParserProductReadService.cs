using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserObservability.Dtos;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IParserProductReadService
{
    Task<ServiceResult<PagedResponse<ParserProductListItemDto>>> GetListAsync(
        ParserProductListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProductFilterOptionsDto>> GetFilterOptionsAsync(
        ParserProductFilterOptionsQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ParserProductCoveredNicheDto>>> GetCoveredNichesAsync(
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserDemoCardOptionsDto>> GetDemoCardOptionsAsync(
        bool includeCharacteristics,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserDemoCardCharacteristicsDto>> GetDemoCardCharacteristicsAsync(
        string subcategory,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProductLogisticsSummaryAggregateDto>> GetLogisticsSummaryAsync(
        ParserProductLogisticsSummaryQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProductDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
