using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Orders.Dtos;

namespace AshmesMarketplaces.Application.Orders.Services;

public interface IOrderService
{
    Task<ServiceResult<PagedResponse<OrderListItemResponse>>> GetListAsync(OrderListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<OrderResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<OrderResponse>> UpdateAsync(Guid id, UpdateOrderRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
