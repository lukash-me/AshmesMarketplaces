using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Products.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Products.Services;

public sealed class ProductHistoryService : IProductHistoryService
{
    private readonly ApplicationDbContext _dbContext;

    public ProductHistoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ProductHistoryListItemResponse>>> GetListAsync(
        Guid productId,
        ProductHistoryListQuery query,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
            return ServiceResult<PagedResponse<ProductHistoryListItemResponse>>.BadRequest("Product id is required.");

        var id = ProductId.Create(productId);
        var productExists = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!productExists)
            return ServiceResult<PagedResponse<ProductHistoryListItemResponse>>.NotFound("Product was not found.");

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var history = _dbContext.ProductHistories
            .AsNoTracking()
            .Where(x => x.IdProduct == id);

        history = query.Sort?.Trim() switch
        {
            "date" => history.OrderBy(x => x.Date),
            _ => history.OrderByDescending(x => x.Date)
        };

        var totalCount = await history.CountAsync(cancellationToken);
        var historyItems = await history
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = historyItems
            .Select(x => new ProductHistoryListItemResponse(
                x.Id,
                x.IdProduct.Value,
                x.Cost,
                x.Price,
                x.Discount,
                x.IsActive,
                x.IsAutoDiscountActive,
                x.Date))
            .ToList();

        return ServiceResult<PagedResponse<ProductHistoryListItemResponse>>.Success(
            new PagedResponse<ProductHistoryListItemResponse>(items, page, pageSize, totalCount));
    }
}
