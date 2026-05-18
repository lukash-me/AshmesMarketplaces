using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.PermissionCategories.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.PermissionCategories.Services;

public sealed class PermissionCategoryService : IPermissionCategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public PermissionCategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<PermissionCategoryListItemResponse>>> GetListAsync(PermissionCategoryListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var categories = _dbContext.PermissionCategories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            categories = categories.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%")));
        }

        categories = query.Sort?.Trim() switch
        {
            "name" => categories.OrderBy(x => x.Name),
            "-name" => categories.OrderByDescending(x => x.Name),
            "dateCreate" => categories.OrderBy(x => x.DateCreate),
            "-dateCreate" => categories.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => categories.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => categories.OrderByDescending(x => x.DateUpdate),
            _ => categories.OrderBy(x => x.Name)
        };

        var totalCount = await categories.CountAsync(cancellationToken);
        var items = await categories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PermissionCategoryListItemResponse(
                x.Id,
                x.Name,
                x.Description,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<PermissionCategoryListItemResponse>>.Success(new PagedResponse<PermissionCategoryListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<PermissionCategoryResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<PermissionCategoryResponse>.BadRequest("Permission category id is required.");

        var category = await _dbContext.PermissionCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return category is null
            ? ServiceResult<PermissionCategoryResponse>.NotFound("Permission category was not found.")
            : ServiceResult<PermissionCategoryResponse>.Success(MapToResponse(category));
    }

    public async Task<ServiceResult<PermissionCategoryResponse>> CreateAsync(CreatePermissionCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var category = new PermissionCategory(
                request.Name,
                request.Description,
                request.DateUpdate,
                request.DateCreate);

            _dbContext.PermissionCategories.Add(category);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<PermissionCategoryResponse>.Success(MapToResponse(category));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<PermissionCategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<PermissionCategoryResponse>.Conflict("Permission category cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<PermissionCategoryResponse>> UpdateAsync(Guid id, UpdatePermissionCategoryRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<PermissionCategoryResponse>.BadRequest("Permission category id is required.");

        var category = await _dbContext.PermissionCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult<PermissionCategoryResponse>.NotFound("Permission category was not found.");

        try
        {
            _ = new PermissionCategory(
                request.Name,
                request.Description,
                request.DateUpdate,
                request.DateCreate);

            var entry = _dbContext.Entry(category);
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<PermissionCategoryResponse>.Success(MapToResponse(category));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<PermissionCategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<PermissionCategoryResponse>.Conflict("Permission category cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Permission category id is required.");

        var category = await _dbContext.PermissionCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult.NotFound("Permission category was not found.");

        _dbContext.PermissionCategories.Remove(category);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Permission category cannot be deleted because it is referenced by other records.");
        }
    }

    private static PermissionCategoryResponse MapToResponse(PermissionCategory category)
    {
        return new PermissionCategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.DateCreate,
            category.DateUpdate);
    }
}
