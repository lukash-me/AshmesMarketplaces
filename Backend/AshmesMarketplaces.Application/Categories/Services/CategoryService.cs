using AshmesMarketplaces.Application.Categories.Dtos;
using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Categories.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public CategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<CategoryListItemResponse>>> GetListAsync(
        ListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var categories = _dbContext.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            categories = categories.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.IdOnMp != null && EF.Functions.ILike(x.IdOnMp, $"%{search}%")));
        }

        categories = query.Sort?.Trim() switch
        {
            "-name" => categories.OrderByDescending(x => x.Name),
            "dateUpdate" => categories.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => categories.OrderByDescending(x => x.DateUpdate),
            _ => categories.OrderBy(x => x.Name)
        };

        var totalCount = await categories.CountAsync(cancellationToken);
        var items = await categories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CategoryListItemResponse(
                x.Id,
                x.IdParentCategory,
                x.IdOnMp,
                x.Name,
                x.Level,
                x.IsActive,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<CategoryListItemResponse>>.Success(
            new PagedResponse<CategoryListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<CategoryResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<CategoryResponse>.BadRequest("Category id is required.");

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return category is null
            ? ServiceResult<CategoryResponse>.NotFound("Category was not found.")
            : ServiceResult<CategoryResponse>.Success(MapToResponse(category));
    }

    public async Task<ServiceResult<CategoryResponse>> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IdParentCategory.HasValue)
        {
            var parentExists = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.IdParentCategory.Value, cancellationToken);

            if (!parentExists)
                return ServiceResult<CategoryResponse>.Conflict("Parent category was not found.");
        }

        try
        {
            var category = new Category(
                request.IdParentCategory,
                request.IdOnMp,
                request.Name,
                request.Level,
                request.IsActive,
                request.DateUpdate);

            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<CategoryResponse>.Success(MapToResponse(category));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<CategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CategoryResponse>.Conflict("Category cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<CategoryResponse>> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<CategoryResponse>.BadRequest("Category id is required.");

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
            return ServiceResult<CategoryResponse>.NotFound("Category was not found.");

        if (request.IdParentCategory.HasValue)
        {
            var parentExists = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.IdParentCategory.Value, cancellationToken);

            if (!parentExists)
                return ServiceResult<CategoryResponse>.Conflict("Parent category was not found.");
        }

        var entry = _dbContext.Entry(category);
        entry.Property(x => x.IdParentCategory).CurrentValue = request.IdParentCategory;
        entry.Property(x => x.IdOnMp).CurrentValue = request.IdOnMp;
        entry.Property(x => x.Name).CurrentValue = request.Name;
        entry.Property(x => x.Level).CurrentValue = request.Level;
        entry.Property(x => x.IsActive).CurrentValue = request.IsActive;
        entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<CategoryResponse>.Success(MapToResponse(category));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<CategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CategoryResponse>.Conflict("Category cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Category id is required.");

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
            return ServiceResult.NotFound("Category was not found.");

        _dbContext.Categories.Remove(category);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Category cannot be deleted because it is referenced by other records.");
        }
    }

    private static CategoryResponse MapToResponse(Category category)
    {
        return new CategoryResponse(
            category.Id,
            category.IdParentCategory,
            category.IdOnMp,
            category.Name,
            category.Level,
            category.IsActive,
            category.DateUpdate);
    }
}
