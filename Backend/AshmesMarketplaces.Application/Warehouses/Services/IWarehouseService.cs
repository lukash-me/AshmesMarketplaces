using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Warehouses.Dtos;

namespace AshmesMarketplaces.Application.Warehouses.Services;

public interface IWarehouseService
{
    Task<ServiceResult<PagedResponse<WarehouseListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<WarehouseResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ServiceResult<WarehouseResponse>> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<WarehouseResponse>> UpdateAsync(
        Guid id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
