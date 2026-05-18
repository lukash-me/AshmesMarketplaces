using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Logistics.Dtos;

namespace AshmesMarketplaces.Application.Logistics.Services;

public interface ILogisticService
{
    Task<ServiceResult<PagedResponse<LogisticListItemResponse>>> GetListAsync(LogisticListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<LogisticResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<LogisticResponse>> CreateAsync(CreateLogisticRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<LogisticResponse>> UpdateAsync(Guid id, UpdateLogisticRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
