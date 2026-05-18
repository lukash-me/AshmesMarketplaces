using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Logistics.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Logistics;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Logistics.Services;

public sealed class LogisticService : ILogisticService
{
    private readonly ApplicationDbContext _dbContext;

    public LogisticService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<LogisticListItemResponse>>> GetListAsync(LogisticListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var logistics = _dbContext.Logistics.AsNoTracking();

        if (query.IdProduct.HasValue)
            logistics = logistics.Where(x => x.IdProduct == ProductId.Create(query.IdProduct.Value));

        if (query.IdWarehouse.HasValue)
            logistics = logistics.Where(x => x.IdWarehouse == query.IdWarehouse.Value);

        if (query.Type.HasValue)
            logistics = logistics.Where(x => x.Type == query.Type.Value);

        if (query.DateFrom.HasValue)
            logistics = logistics.Where(x => x.Date >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            logistics = logistics.Where(x => x.Date <= query.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            logistics = logistics.Where(x =>
                x.IdWarehouse.HasValue
                && _dbContext.Warehouses.Any(w =>
                    w.Id == x.IdWarehouse.Value
                    && (EF.Functions.ILike(w.Name, $"%{search}%")
                        || EF.Functions.ILike(w.Code, $"%{search}%")
                        || EF.Functions.ILike(w.Region, $"%{search}%")
                        || (w.City != null && EF.Functions.ILike(w.City, $"%{search}%")))));
        }

        logistics = query.Sort?.Trim() switch
        {
            "date" => logistics.OrderBy(x => x.Date),
            "-date" => logistics.OrderByDescending(x => x.Date),
            "stockAmountStatistic" => logistics.OrderBy(x => x.StockAmountStatistic),
            "-stockAmountStatistic" => logistics.OrderByDescending(x => x.StockAmountStatistic),
            _ => logistics.OrderByDescending(x => x.Date)
        };

        var totalCount = await logistics.CountAsync(cancellationToken);
        var items = await logistics
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LogisticListItemResponse(
                x.Id,
                x.IdProduct.Value,
                x.IdWarehouse,
                x.StockAmount,
                x.StockAmountStatistic,
                x.StockInTransit,
                x.CostStorage,
                x.CostLogistic,
                x.Type,
                x.Date))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<LogisticListItemResponse>>.Success(new PagedResponse<LogisticListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<LogisticResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<LogisticResponse>.BadRequest("Logistic id is required.");

        var logistic = await _dbContext.Logistics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return logistic is null
            ? ServiceResult<LogisticResponse>.NotFound("Logistic was not found.")
            : ServiceResult<LogisticResponse>.Success(MapToResponse(logistic));
    }

    public async Task<ServiceResult<LogisticResponse>> CreateAsync(CreateLogisticRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdProduct, request.IdWarehouse, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<LogisticResponse>.Conflict(referenceCheck);

        try
        {
            var logistic = new Logistic(
                ProductId.Create(request.IdProduct),
                request.IdWarehouse,
                request.StockAmount,
                request.StockAmountStatistic,
                request.StockInTransit,
                request.CostStorage,
                request.CostLogistic,
                request.Type,
                request.Date);

            _dbContext.Logistics.Add(logistic);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<LogisticResponse>.Success(MapToResponse(logistic));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<LogisticResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<LogisticResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<LogisticResponse>.Conflict("Logistic cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<LogisticResponse>> UpdateAsync(Guid id, UpdateLogisticRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<LogisticResponse>.BadRequest("Logistic id is required.");

        var logistic = await _dbContext.Logistics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (logistic is null)
            return ServiceResult<LogisticResponse>.NotFound("Logistic was not found.");

        var referenceCheck = await ValidateReferencesAsync(request.IdProduct, request.IdWarehouse, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<LogisticResponse>.Conflict(referenceCheck);

        try
        {
            _ = new Logistic(
                ProductId.Create(request.IdProduct),
                request.IdWarehouse,
                request.StockAmount,
                request.StockAmountStatistic,
                request.StockInTransit,
                request.CostStorage,
                request.CostLogistic,
                request.Type,
                request.Date);

            var entry = _dbContext.Entry(logistic);
            entry.Property(x => x.IdProduct).CurrentValue = ProductId.Create(request.IdProduct);
            entry.Property(x => x.IdWarehouse).CurrentValue = request.IdWarehouse;
            entry.Property(x => x.StockAmount).CurrentValue = request.StockAmount;
            entry.Property(x => x.StockAmountStatistic).CurrentValue = request.StockAmountStatistic;
            entry.Property(x => x.StockInTransit).CurrentValue = request.StockInTransit;
            entry.Property(x => x.CostStorage).CurrentValue = request.CostStorage;
            entry.Property(x => x.CostLogistic).CurrentValue = request.CostLogistic;
            entry.Property(x => x.Type).CurrentValue = request.Type;
            entry.Property(x => x.Date).CurrentValue = request.Date;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<LogisticResponse>.Success(MapToResponse(logistic));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<LogisticResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<LogisticResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<LogisticResponse>.Conflict("Logistic cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Logistic id is required.");

        var logistic = await _dbContext.Logistics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (logistic is null)
            return ServiceResult.NotFound("Logistic was not found.");

        _dbContext.Logistics.Remove(logistic);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Logistic cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(Guid idProduct, Guid? idWarehouse, CancellationToken cancellationToken)
    {
        var productExists = await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == ProductId.Create(idProduct), cancellationToken);
        if (!productExists)
            return "Product was not found.";

        if (idWarehouse.HasValue)
        {
            var warehouseExists = await _dbContext.Warehouses.AsNoTracking().AnyAsync(x => x.Id == idWarehouse.Value, cancellationToken);
            if (!warehouseExists)
                return "Warehouse was not found.";
        }

        return null;
    }

    private static LogisticResponse MapToResponse(Logistic logistic)
    {
        return new LogisticResponse(
            logistic.Id,
            logistic.IdProduct.Value,
            logistic.IdWarehouse,
            logistic.StockAmount,
            logistic.StockAmountStatistic,
            logistic.StockInTransit,
            logistic.CostStorage,
            logistic.CostLogistic,
            logistic.Type,
            logistic.Date);
    }
}
