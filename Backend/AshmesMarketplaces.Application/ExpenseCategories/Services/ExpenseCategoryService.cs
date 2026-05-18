using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ExpenseCategories.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.ExpenseCategories.Services;

public sealed class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public ExpenseCategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ExpenseCategoryListItemResponse>>> GetListAsync(ExpenseCategoryListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var categories = _dbContext.ExpenseCategories.AsNoTracking();

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
            .Select(x => new ExpenseCategoryListItemResponse(
                x.Id,
                x.Name,
                x.Description,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<ExpenseCategoryListItemResponse>>.Success(new PagedResponse<ExpenseCategoryListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ExpenseCategoryResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ExpenseCategoryResponse>.BadRequest("Expense category id is required.");

        var category = await _dbContext.ExpenseCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return category is null
            ? ServiceResult<ExpenseCategoryResponse>.NotFound("Expense category was not found.")
            : ServiceResult<ExpenseCategoryResponse>.Success(MapToResponse(category));
    }

    public async Task<ServiceResult<ExpenseCategoryResponse>> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var category = new ExpenseCategory(
                request.Name,
                request.Description,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.ExpenseCategories.Add(category);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ExpenseCategoryResponse>.Success(MapToResponse(category));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ExpenseCategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ExpenseCategoryResponse>.Conflict("Expense category cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<ExpenseCategoryResponse>> UpdateAsync(Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ExpenseCategoryResponse>.BadRequest("Expense category id is required.");

        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult<ExpenseCategoryResponse>.NotFound("Expense category was not found.");

        try
        {
            _ = new ExpenseCategory(
                request.Name,
                request.Description,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(category);
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ExpenseCategoryResponse>.Success(MapToResponse(category));
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ExpenseCategoryResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ExpenseCategoryResponse>.Conflict("Expense category cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Expense category id is required.");

        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult.NotFound("Expense category was not found.");

        _dbContext.ExpenseCategories.Remove(category);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Expense category cannot be deleted because it is referenced by other records.");
        }
    }

    private static ExpenseCategoryResponse MapToResponse(ExpenseCategory category)
    {
        return new ExpenseCategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.DateCreate,
            category.DateUpdate);
    }
}
