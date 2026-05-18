using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Expenses.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Expenses.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _dbContext;

    public ExpenseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<PagedResponse<ExpenseListItemResponse>>> GetListAsync(ExpenseListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var expenses = _dbContext.Expenses.AsNoTracking();

        if (query.IdWorkspace.HasValue)
            expenses = expenses.Where(x => x.IdWorkspace == query.IdWorkspace.Value);

        if (query.IdCategory.HasValue)
            expenses = expenses.Where(x => x.IdCategory == query.IdCategory.Value);

        if (query.IdCreator.HasValue)
            expenses = expenses.Where(x => x.IdCreator == query.IdCreator.Value);

        if (query.IdResponsible.HasValue)
            expenses = expenses.Where(x => x.IdResponsible == query.IdResponsible.Value);

        if (query.Status.HasValue)
            expenses = expenses.Where(x => x.Status == query.Status.Value);

        if (query.DatePayFrom.HasValue)
            expenses = expenses.Where(x => x.DatePay >= query.DatePayFrom.Value);

        if (query.DatePayTo.HasValue)
            expenses = expenses.Where(x => x.DatePay <= query.DatePayTo.Value);

        if (query.DateCreateFrom.HasValue)
            expenses = expenses.Where(x => x.DateCreate >= query.DateCreateFrom.Value);

        if (query.DateCreateTo.HasValue)
            expenses = expenses.Where(x => x.DateCreate <= query.DateCreateTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            expenses = expenses.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || (x.Description != null && EF.Functions.ILike(x.Description, $"%{search}%"))
                || (x.IdCategory.HasValue
                    && _dbContext.ExpenseCategories.Any(c =>
                        c.Id == x.IdCategory.Value
                        && EF.Functions.ILike(c.Name, $"%{search}%"))));
        }

        expenses = query.Sort?.Trim() switch
        {
            "name" => expenses.OrderBy(x => x.Name),
            "-name" => expenses.OrderByDescending(x => x.Name),
            "cost" => expenses.OrderBy(x => x.Cost),
            "-cost" => expenses.OrderByDescending(x => x.Cost),
            "datePay" => expenses.OrderBy(x => x.DatePay),
            "-datePay" => expenses.OrderByDescending(x => x.DatePay),
            "dateCreate" => expenses.OrderBy(x => x.DateCreate),
            "-dateCreate" => expenses.OrderByDescending(x => x.DateCreate),
            "dateUpdate" => expenses.OrderBy(x => x.DateUpdate),
            "-dateUpdate" => expenses.OrderByDescending(x => x.DateUpdate),
            _ => expenses.OrderByDescending(x => x.DateUpdate)
        };

        var totalCount = await expenses.CountAsync(cancellationToken);
        var items = await expenses
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ExpenseListItemResponse(
                x.Id,
                x.IdWorkspace,
                x.IdCategory,
                x.IdCreator,
                x.IdResponsible,
                x.Name,
                x.Cost,
                x.Status,
                x.DatePay,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResponse<ExpenseListItemResponse>>.Success(new PagedResponse<ExpenseListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ExpenseResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ExpenseResponse>.BadRequest("Expense id is required.");

        var expense = await _dbContext.Expenses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return expense is null
            ? ServiceResult<ExpenseResponse>.NotFound("Expense was not found.")
            : ServiceResult<ExpenseResponse>.Success(MapToResponse(expense));
    }

    public async Task<ServiceResult<ExpenseResponse>> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var referenceCheck = await ValidateReferencesAsync(request.IdWorkspace, request.IdCategory, request.IdCreator, request.IdResponsible, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<ExpenseResponse>.Conflict(referenceCheck);

        try
        {
            var expense = new Expense(
                request.IdWorkspace,
                request.IdCategory,
                request.IdCreator,
                request.IdResponsible,
                request.Name,
                request.Description,
                request.Cost,
                request.Status,
                request.DatePay,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Expenses.Add(expense);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ExpenseResponse>.Success(MapToResponse(expense));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<ExpenseResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ExpenseResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ExpenseResponse>.Conflict("Expense cannot be created because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult<ExpenseResponse>> UpdateAsync(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ExpenseResponse>.BadRequest("Expense id is required.");

        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null)
            return ServiceResult<ExpenseResponse>.NotFound("Expense was not found.");

        var referenceCheck = await ValidateReferencesAsync(request.IdWorkspace, request.IdCategory, request.IdCreator, request.IdResponsible, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<ExpenseResponse>.Conflict(referenceCheck);

        try
        {
            _ = new Expense(
                request.IdWorkspace,
                request.IdCategory,
                request.IdCreator,
                request.IdResponsible,
                request.Name,
                request.Description,
                request.Cost,
                request.Status,
                request.DatePay,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(expense);
            entry.Property(x => x.IdWorkspace).CurrentValue = request.IdWorkspace;
            entry.Property(x => x.IdCategory).CurrentValue = request.IdCategory;
            entry.Property(x => x.IdCreator).CurrentValue = request.IdCreator;
            entry.Property(x => x.IdResponsible).CurrentValue = request.IdResponsible;
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.Cost).CurrentValue = request.Cost;
            entry.Property(x => x.Status).CurrentValue = request.Status;
            entry.Property(x => x.DatePay).CurrentValue = request.DatePay;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ExpenseResponse>.Success(MapToResponse(expense));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ServiceResult<ExpenseResponse>.BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResult<ExpenseResponse>.BadRequest(exception.Message);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ExpenseResponse>.Conflict("Expense cannot be updated because it conflicts with database constraints.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest("Expense id is required.");

        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null)
            return ServiceResult.NotFound("Expense was not found.");

        _dbContext.Expenses.Remove(expense);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Expense cannot be deleted because it is referenced by other records.");
        }
    }

    private async Task<string?> ValidateReferencesAsync(
        Guid idWorkspace,
        Guid? idCategory,
        Guid idCreator,
        Guid? idResponsible,
        CancellationToken cancellationToken)
    {
        var workspaceExists = await _dbContext.Workspaces.AsNoTracking().AnyAsync(x => x.Id == idWorkspace, cancellationToken);
        if (!workspaceExists)
            return "Workspace was not found.";

        if (idCategory.HasValue)
        {
            var categoryExists = await _dbContext.ExpenseCategories.AsNoTracking().AnyAsync(x => x.Id == idCategory.Value, cancellationToken);
            if (!categoryExists)
                return "Expense category was not found.";
        }

        var creatorExists = await _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == idCreator, cancellationToken);
        if (!creatorExists)
            return "Creator user was not found.";

        if (idResponsible.HasValue)
        {
            var responsibleExists = await _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == idResponsible.Value, cancellationToken);
            if (!responsibleExists)
                return "Responsible user was not found.";
        }

        return null;
    }

    private static ExpenseResponse MapToResponse(Expense expense)
    {
        return new ExpenseResponse(
            expense.Id,
            expense.IdWorkspace,
            expense.IdCategory,
            expense.IdCreator,
            expense.IdResponsible,
            expense.Name,
            expense.Description,
            expense.Cost,
            expense.Status,
            expense.DatePay,
            expense.DateCreate,
            expense.DateUpdate);
    }
}
