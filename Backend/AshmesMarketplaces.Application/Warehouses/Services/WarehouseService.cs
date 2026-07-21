using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Warehouses.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Warehouses.Services;

public sealed class WarehouseService : IWarehouseService
{
    private readonly ApplicationDbContext _dbContext;

    public WarehouseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<WarehouseListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var warehouses = _dbContext.Warehouses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            warehouses = warehouses.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.Code, $"%{search}%")
                || EF.Functions.ILike(x.Region, $"%{search}%")
                || (x.City != null && EF.Functions.ILike(x.City, $"%{search}%")));
        }

        warehouses = query.Sort?.Trim() switch
        {
            "-name" => warehouses.OrderByDescending(x => x.Name),
            "dateUpdate" => warehouses.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => warehouses.OrderByDescending(x => x.DateUpdate),
            _ => warehouses.OrderBy(x => x.Name)
        };

        var totalCount = await warehouses.CountAsync(cancellationToken);
        var items = await warehouses
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WarehouseListItemResponse(
                x.Id,
                x.IdMp,
                x.Name,
                x.Code,
                x.Region,
                x.City,
                x.IsActive,
                x.Type,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<WarehouseListItemResponse>>.Success(
            new PagedResponse<WarehouseListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<WarehouseResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<WarehouseResponse>.BadRequest("Warehouse id is required.");

        var warehouse = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return warehouse is null
            ? ServiceResult<WarehouseResponse>.NotFound("Warehouse was not found.")
            : ServiceResult<WarehouseResponse>.Success(MapToResponse(warehouse));
    }

    public async Task<ServiceResult<WarehouseResponse>> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var marketplaceExists = await _dbContext.Marketplaces
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.IdMp, cancellationToken);

        if (!marketplaceExists)
            return ServiceResult<WarehouseResponse>.Conflict("Marketplace was not found.");

        try
        {
            var warehouse = new Warehouse(
                request.IdMp,
                request.Name,
                request.Code,
                request.Region,
                request.City,
                request.Address,
                request.Latitude,
                request.Longitude,
                request.IsActive,
                request.Type,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<WarehouseResponse>.Success(MapToResponse(warehouse));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<WarehouseResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<WarehouseResponse>.Conflict("Warehouse cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<WarehouseResponse>> UpdateAsync(
        Guid id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<WarehouseResponse>.BadRequest("Warehouse id is required.");

        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (warehouse is null)
            return ServiceResult<WarehouseResponse>.NotFound("Warehouse was not found.");

        var marketplaceExists = await _dbContext.Marketplaces
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.IdMp, cancellationToken);

        if (!marketplaceExists)
            return ServiceResult<WarehouseResponse>.Conflict("Marketplace was not found.");

        var entry = _dbContext.Entry(warehouse);
        entry.Property(x => x.IdMp).CurrentValue = request.IdMp;
        entry.Property(x => x.Name).CurrentValue = request.Name;
        entry.Property(x => x.Code).CurrentValue = request.Code;
        entry.Property(x => x.Region).CurrentValue = request.Region;
        entry.Property(x => x.City).CurrentValue = request.City;
        entry.Property(x => x.Address).CurrentValue = request.Address;
        entry.Property(x => x.Latitude).CurrentValue = request.Latitude;
        entry.Property(x => x.Longitude).CurrentValue = request.Longitude;
        entry.Property(x => x.IsActive).CurrentValue = request.IsActive;
        entry.Property(x => x.Type).CurrentValue = request.Type;
        entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
        entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<WarehouseResponse>.Success(MapToResponse(warehouse));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<WarehouseResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<WarehouseResponse>.Conflict("Warehouse cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Warehouse id is required.");

        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (warehouse is null)
            return ServiceResult.NotFound("Warehouse was not found.");

        _dbContext.Warehouses.Remove(warehouse);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Warehouse cannot be deleted because it is referenced by other records.");
        }
    }

    private static WarehouseResponse MapToResponse(Warehouse warehouse)
    {
        return new WarehouseResponse(
            warehouse.Id,
            warehouse.IdMp,
            warehouse.Name,
            warehouse.Code,
            warehouse.Region,
            warehouse.City,
            warehouse.Address,
            warehouse.Latitude,
            warehouse.Longitude,
            warehouse.IsActive,
            warehouse.Type,
            warehouse.DateCreate,
            warehouse.DateUpdate);
    }
}
