using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Marketplaces.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Marketplaces.Services;

public sealed class MarketplaceService : IMarketplaceService
{
    private readonly ApplicationDbContext _dbContext;

    public MarketplaceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<MarketplaceListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var marketplaces = _dbContext.Marketplaces.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            marketplaces = marketplaces.Where(x => EF.Functions.ILike(x.Name, $"%{search}%"));
        }

        marketplaces = query.Sort?.Trim() switch
        {
            "-name" => marketplaces.OrderByDescending(x => x.Name),
            "dateUpdate" => marketplaces.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => marketplaces.OrderByDescending(x => x.DateUpdate),
            _ => marketplaces.OrderBy(x => x.Name)
        };

        var totalCount = await marketplaces.CountAsync(cancellationToken);
        var items = await marketplaces
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MarketplaceListItemResponse(
                x.Id,
                x.Name,
                x.Currency,
                x.Region,
                x.TypeCommission,
                x.SchemeDelivery,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<MarketplaceListItemResponse>>.Success(
            new PagedResponse<MarketplaceListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<MarketplaceResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<MarketplaceResponse>.BadRequest("Marketplace id is required.");

        var marketplace = await _dbContext.Marketplaces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return marketplace is null
            ? ServiceResult<MarketplaceResponse>.NotFound("Marketplace was not found.")
            : ServiceResult<MarketplaceResponse>.Success(MapToResponse(marketplace));
    }

    public async Task<ServiceResult<MarketplaceResponse>> CreateAsync(
        CreateMarketplaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var marketplace = new Marketplace(
                request.Name,
                request.ApiUrl,
                request.ApiVersion,
                request.Currency,
                request.Region,
                request.TypeCommission,
                request.SchemeDelivery,
                request.DateUpdate);

            _dbContext.Marketplaces.Add(marketplace);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<MarketplaceResponse>.Success(MapToResponse(marketplace));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<MarketplaceResponse>.BadRequest(exception.Message);
        }
    }

    public async Task<ServiceResult<MarketplaceResponse>> UpdateAsync(
        Guid id,
        UpdateMarketplaceRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<MarketplaceResponse>.BadRequest("Marketplace id is required.");

        var marketplace = await _dbContext.Marketplaces
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (marketplace is null)
            return ServiceResult<MarketplaceResponse>.NotFound("Marketplace was not found.");

        var entry = _dbContext.Entry(marketplace);
        entry.Property(x => x.Name).CurrentValue = request.Name;
        entry.Property(x => x.ApiUrl).CurrentValue = request.ApiUrl;
        entry.Property(x => x.ApiVersion).CurrentValue = request.ApiVersion;
        entry.Property(x => x.Currency).CurrentValue = request.Currency;
        entry.Property(x => x.Region).CurrentValue = request.Region;
        entry.Property(x => x.TypeCommission).CurrentValue = request.TypeCommission;
        entry.Property(x => x.SchemeDelivery).CurrentValue = request.SchemeDelivery;
        entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<MarketplaceResponse>.Success(MapToResponse(marketplace));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<MarketplaceResponse>.BadRequest(exception.Message);
        }
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Marketplace id is required.");

        var marketplace = await _dbContext.Marketplaces
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (marketplace is null)
            return ServiceResult.NotFound("Marketplace was not found.");

        _dbContext.Marketplaces.Remove(marketplace);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Marketplace cannot be deleted because it is referenced by other records.");
        }
    }

    private static MarketplaceResponse MapToResponse(Marketplace marketplace)
    {
        return new MarketplaceResponse(
            marketplace.Id,
            marketplace.Name,
            marketplace.ApiUrl,
            marketplace.ApiVersion,
            marketplace.Currency,
            marketplace.Region,
            marketplace.TypeCommission,
            marketplace.SchemeDelivery,
            marketplace.DateUpdate);
    }
}
