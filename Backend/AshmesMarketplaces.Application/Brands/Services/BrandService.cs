using AshmesMarketplaces.Application.Brands.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Brands.Services;

public sealed class BrandService : IBrandService
{
    private readonly ApplicationDbContext _dbContext;

    public BrandService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<BrandListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var brands = _dbContext.Brands.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            brands = brands.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.ILike(x.Country, $"%{search}%")
                || EF.Functions.ILike(x.Manufacturer, $"%{search}%"));
        }

        brands = query.Sort?.Trim() switch
        {
            "-name" => brands.OrderByDescending(x => x.Name),
            "dateUpdate" => brands.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => brands.OrderByDescending(x => x.DateUpdate),
            _ => brands.OrderBy(x => x.Name)
        };

        var totalCount = await brands.CountAsync(cancellationToken);
        var items = await brands
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BrandListItemResponse(
                x.Id,
                x.Name,
                x.IsVerified,
                x.Country,
                x.Manufacturer,
                x.SalesAmount,
                x.RateRedemption,
                x.Level,
                x.Type,
                x.DateMpRegistration,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<BrandListItemResponse>>.Success(
            new PagedResponse<BrandListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<BrandResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<BrandResponse>.BadRequest("Brand id is required.");

        var brand = await _dbContext.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return brand is null
            ? ServiceResult<BrandResponse>.NotFound("Brand was not found.")
            : ServiceResult<BrandResponse>.Success(MapToResponse(brand));
    }

    public async Task<ServiceResult<BrandResponse>> CreateAsync(
        CreateBrandRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var brand = new Brand(
                request.Name,
                request.IsVerified,
                request.Country,
                request.Manufacturer,
                request.SalesAmount,
                request.RateRedemption,
                request.Level,
                request.Type,
                request.DateMpRegistration,
                request.DateUpdate);

            _dbContext.Brands.Add(brand);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<BrandResponse>.Success(MapToResponse(brand));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<BrandResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<BrandResponse>.Conflict("Brand cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<BrandResponse>> UpdateAsync(
        Guid id,
        UpdateBrandRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<BrandResponse>.BadRequest("Brand id is required.");

        var brand = await _dbContext.Brands
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (brand is null)
            return ServiceResult<BrandResponse>.NotFound("Brand was not found.");

        var entry = _dbContext.Entry(brand);
        entry.Property(x => x.Name).CurrentValue = request.Name;
        entry.Property(x => x.IsVerified).CurrentValue = request.IsVerified;
        entry.Property(x => x.Country).CurrentValue = request.Country;
        entry.Property(x => x.Manufacturer).CurrentValue = request.Manufacturer;
        entry.Property(x => x.SalesAmount).CurrentValue = request.SalesAmount;
        entry.Property(x => x.RateRedemption).CurrentValue = request.RateRedemption;
        entry.Property(x => x.Level).CurrentValue = request.Level;
        entry.Property(x => x.Type).CurrentValue = request.Type;
        entry.Property(x => x.DateMpRegistration).CurrentValue = request.DateMpRegistration;
        entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<BrandResponse>.Success(MapToResponse(brand));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<BrandResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<BrandResponse>.Conflict("Brand cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Brand id is required.");

        var brand = await _dbContext.Brands
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (brand is null)
            return ServiceResult.NotFound("Brand was not found.");

        _dbContext.Brands.Remove(brand);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Brand cannot be deleted because it is referenced by other records.");
        }
    }

    private static BrandResponse MapToResponse(Brand brand)
    {
        return new BrandResponse(
            brand.Id,
            brand.Name,
            brand.IsVerified,
            brand.Country,
            brand.Manufacturer,
            brand.SalesAmount,
            brand.RateRedemption,
            brand.Level,
            brand.Type,
            brand.DateMpRegistration,
            brand.DateUpdate);
    }
}
