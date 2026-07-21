using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Orders.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Orders;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Orders.Services;

public sealed class OrderService : IOrderService
{
    private readonly ApplicationDbContext _dbContext;

    public OrderService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<OrderListItemResponse>>> GetListAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var orders = _dbContext.Orders.AsNoTracking();

        if (query.IdProduct.HasValue)
            orders = orders.Where(x => x.IdProduct == ProductId.Create(query.IdProduct.Value));

        if (query.Status.HasValue)
            orders = orders.Where(x => x.Status == query.Status.Value);

        if (query.DateOpenedFrom.HasValue)
            orders = orders.Where(x => x.DateOpened >= query.DateOpenedFrom.Value);

        if (query.DateOpenedTo.HasValue)
            orders = orders.Where(x => x.DateOpened <= query.DateOpenedTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(x =>
                (x.LocationSource != null && EF.Functions.ILike(x.LocationSource, $"%{search}%"))
                || (x.LocationDestination != null && EF.Functions.ILike(x.LocationDestination, $"%{search}%")));
        }

        orders = query.Sort?.Trim() switch
        {
            "dateOpened" => orders.OrderBy(x => x.DateOpened),
            "-dateOpened" => orders.OrderByDescending(x => x.DateOpened),
            "dateUpdate" => orders.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => orders.OrderByDescending(x => x.DateUpdate),
            "price" => orders.OrderBy(x => x.Price),
            "-price" => orders.OrderByDescending(x => x.Price),
            "amount" => orders.OrderBy(x => x.Amount),
            "-amount" => orders.OrderByDescending(x => x.Amount),
            _ => orders.OrderByDescending(x => x.DateOpened)
        };

        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrderListItemResponse(
                x.Id,
                x.IdProduct.Value,
                x.Price,
                x.Discount,
                x.Amount,
                x.LocationSource,
                x.LocationDestination,
                x.Status,
                x.DateDelivered,
                x.DateOpened,
                x.DateClosed,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<OrderListItemResponse>>.Success(new PagedResponse<OrderListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<OrderResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<OrderResponse>.BadRequest("Order id is required.");

        var order = await _dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null
            ? ServiceResult<OrderResponse>.NotFound("Order was not found.")
            : ServiceResult<OrderResponse>.Success(MapToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(request.IdProduct, cancellationToken))
            return ServiceResult<OrderResponse>.Conflict("Product was not found.");

        try
        {
            var order = new Order(
                ProductId.Create(request.IdProduct),
                request.Price,
                request.Discount,
                request.Amount,
                request.LocationSource,
                request.LocationDestination,
                request.Status,
                request.DateDelivered,
                request.DateOpened,
                request.DateClosed,
                request.DateUpdate);

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<OrderResponse>.Success(MapToResponse(order));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<OrderResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<OrderResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<OrderResponse>.Conflict("Order cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<OrderResponse>> UpdateAsync(Guid id, UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<OrderResponse>.BadRequest("Order id is required.");

        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null)
            return ServiceResult<OrderResponse>.NotFound("Order was not found.");

        if (!await ProductExistsAsync(request.IdProduct, cancellationToken))
            return ServiceResult<OrderResponse>.Conflict("Product was not found.");

        try
        {
            _ = new Order(
                ProductId.Create(request.IdProduct),
                request.Price,
                request.Discount,
                request.Amount,
                request.LocationSource,
                request.LocationDestination,
                request.Status,
                request.DateDelivered,
                request.DateOpened,
                request.DateClosed,
                request.DateUpdate);

            var entry = _dbContext.Entry(order);
            entry.Property(x => x.IdProduct).CurrentValue = ProductId.Create(request.IdProduct);
            entry.Property(x => x.Price).CurrentValue = request.Price;
            entry.Property(x => x.Discount).CurrentValue = request.Discount;
            entry.Property(x => x.Amount).CurrentValue = request.Amount;
            entry.Property(x => x.LocationSource).CurrentValue = request.LocationSource;
            entry.Property(x => x.LocationDestination).CurrentValue = request.LocationDestination;
            entry.Property(x => x.Status).CurrentValue = request.Status;
            entry.Property(x => x.DateDelivered).CurrentValue = request.DateDelivered;
            entry.Property(x => x.DateOpened).CurrentValue = request.DateOpened;
            entry.Property(x => x.DateClosed).CurrentValue = request.DateClosed;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<OrderResponse>.Success(MapToResponse(order));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<OrderResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<OrderResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<OrderResponse>.Conflict("Order cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Order id is required.");

        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null)
            return ServiceResult.NotFound("Order was not found.");

        _dbContext.Orders.Remove(order);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Order cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<bool> ProductExistsAsync(Guid idProduct, CancellationToken cancellationToken)
    {
        return await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == ProductId.Create(idProduct), cancellationToken);
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.IdProduct.Value,
            order.Price,
            order.Discount,
            order.Amount,
            order.LocationSource,
            order.LocationDestination,
            order.Status,
            order.DateDelivered,
            order.DateOpened,
            order.DateClosed,
            order.DateUpdate);
    }
}
