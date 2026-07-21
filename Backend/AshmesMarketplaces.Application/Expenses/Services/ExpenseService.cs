using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Expenses.Dtos;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.Expenses.Services;

public sealed class ExpenseService : IExpenseService
{
    private const int PlannedStatus = 0;
    private const int PendingPaymentStatus = 1;
    private const int PaidStatus = 2;
    private const int CancelledStatus = 3;

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ExpenseService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<PagedResponse<ExpenseListItemResponse>>> GetListAsync(ExpenseListQuery query, CancellationToken cancellationToken)
    {
        var statusCheck = TryMapStatusKey(query.StatusKey, out var mappedStatus);
        if (!statusCheck)
            return ServiceResult<PagedResponse<ExpenseListItemResponse>>.BadRequest("StatusKey must be one of: planned, pending_payment, paid, cancelled.");

        var access = await EnsureWorkspaceAccessAsync(query.IdWorkspace, cancellationToken);
        if (!access.IsSuccess)
            return ToResult<PagedResponse<ExpenseListItemResponse>>(access);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var expenses = ApplyFilters(_dbContext.Expenses.AsNoTracking(), query, mappedStatus);

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
        var rows = await expenses
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ExpenseProjection(
                x.Id,
                x.IdWorkspace,
                x.IdCategory,
                x.IdCreator,
                x.IdResponsible,
                x.Name,
                x.Description,
                x.Cost,
                x.Status,
                _dbContext.ExpenseCategories
                    .Where(category => x.IdCategory.HasValue && category.Id == x.IdCategory.Value)
                    .Select(category => category.Name)
                    .FirstOrDefault(),
                _dbContext.Workspaces
                    .Where(workspace => workspace.Id == x.IdWorkspace)
                    .Select(workspace => workspace.Name)
                    .FirstOrDefault() ?? string.Empty,
                _dbContext.Users
                    .Where(user => user.Id == x.IdCreator)
                    .Select(user => user.Login)
                    .FirstOrDefault() ?? string.Empty,
                _dbContext.Users
                    .Where(user => user.Id == x.IdCreator)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                _dbContext.Users
                    .Where(user => x.IdResponsible.HasValue && user.Id == x.IdResponsible.Value)
                    .Select(user => user.Login)
                    .FirstOrDefault(),
                _dbContext.Users
                    .Where(user => x.IdResponsible.HasValue && user.Id == x.IdResponsible.Value)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                x.DatePay,
                x.DateCreate,
                x.DateUpdate))
            .ToListAsync(cancellationToken);

        var items = rows.Select(MapToListItemResponse).ToList();

        return ServiceResult<PagedResponse<ExpenseListItemResponse>>.Success(new PagedResponse<ExpenseListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<ExpenseSummaryResponse>> GetSummaryAsync(ExpenseListQuery query, CancellationToken cancellationToken)
    {
        var statusCheck = TryMapStatusKey(query.StatusKey, out var mappedStatus);
        if (!statusCheck)
            return ServiceResult<ExpenseSummaryResponse>.BadRequest("StatusKey must be one of: planned, pending_payment, paid, cancelled.");

        var access = await EnsureWorkspaceAccessAsync(query.IdWorkspace, cancellationToken);
        if (!access.IsSuccess)
            return ToResult<ExpenseSummaryResponse>(access);

        var expenses = ApplyFilters(_dbContext.Expenses.AsNoTracking(), query, mappedStatus);
        var rows = await expenses
            .Select(x => new
            {
                x.Status,
                x.Cost,
                x.DatePay,
                CategoryKey = x.IdCategory.HasValue ? x.IdCategory.Value.ToString() : "none",
                CategoryName = _dbContext.ExpenseCategories
                    .Where(category => x.IdCategory.HasValue && category.Id == x.IdCategory.Value)
                    .Select(category => category.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var totalAmount = rows.Sum(x => x.Cost ?? 0);
        var paidRows = rows.Where(x => x.Status == PaidStatus).ToList();
        var pendingRows = rows.Where(x => x.Status == PendingPaymentStatus).ToList();
        var nonNullCosts = rows.Where(x => x.Cost.HasValue).Select(x => x.Cost!.Value).ToList();

        var byCategory = rows
            .GroupBy(x => new { x.CategoryKey, Label = string.IsNullOrWhiteSpace(x.CategoryName) ? "Без категории" : x.CategoryName })
            .OrderByDescending(group => group.Sum(x => x.Cost ?? 0))
            .ThenBy(group => group.Key.Label)
            .Select(group => new ExpenseSummaryBucketResponse(
                group.Key.CategoryKey,
                group.Key.Label!,
                group.Count(),
                group.Sum(x => x.Cost ?? 0)))
            .ToList();

        var byStatus = rows
            .GroupBy(x => x.Status)
            .OrderBy(group => group.Key)
            .Select(group => new ExpenseSummaryBucketResponse(
                GetStatusKey(group.Key) ?? group.Key.ToString(),
                GetStatusLabel(group.Key),
                group.Count(),
                group.Sum(x => x.Cost ?? 0)))
            .ToList();

        var byMonth = rows
            .Where(x => x.DatePay.HasValue)
            .GroupBy(x => new DateTime(x.DatePay!.Value.Year, x.DatePay.Value.Month, 1))
            .OrderBy(group => group.Key)
            .Select(group => new ExpenseSummaryBucketResponse(
                group.Key.ToString("yyyy-MM"),
                group.Key.ToString("MM.yyyy"),
                group.Count(),
                group.Sum(x => x.Cost ?? 0)))
            .ToList();

        return ServiceResult<ExpenseSummaryResponse>.Success(new ExpenseSummaryResponse(
            rows.Count,
            totalAmount,
            paidRows.Count,
            paidRows.Sum(x => x.Cost ?? 0),
            pendingRows.Count,
            pendingRows.Sum(x => x.Cost ?? 0),
            rows.Count(x => x.Status == PlannedStatus),
            rows.Count(x => x.Status == CancelledStatus),
            rows.Count(x => !x.DatePay.HasValue),
            nonNullCosts.Count == 0 ? null : nonNullCosts.Average(),
            rows.Where(x => x.DatePay.HasValue).Select(x => x.DatePay).DefaultIfEmpty().Max(),
            byCategory,
            byStatus,
            byMonth));
    }

    public async Task<ServiceResult<ExpenseResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return ServiceResult<ExpenseResponse>.BadRequest("Expense id is required.");

        var row = await GetProjectionByIdAsync(id, cancellationToken);
        if (row is not null)
        {
            var access = await EnsureWorkspaceAccessAsync(row.IdWorkspace, cancellationToken);
            if (!access.IsSuccess)
                return ToResult<ExpenseResponse>(access);
        }

        return row is null
            ? ServiceResult<ExpenseResponse>.NotFound("Expense was not found.")
            : ServiceResult<ExpenseResponse>.Success(MapToResponse(row));
    }

    public async Task<ServiceResult<ExpenseResponse>> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var statusCheck = TryMapStatusKey(request.StatusKey, out var mappedStatus);
        if (!statusCheck)
            return ServiceResult<ExpenseResponse>.BadRequest("StatusKey must be one of: planned, pending_payment, paid, cancelled.");

        var access = await EnsureWorkspaceAccessAsync(request.IdWorkspace, cancellationToken);
        if (!access.IsSuccess)
            return ToResult<ExpenseResponse>(access);

        var referenceCheck = await ValidateReferencesAsync(request.IdWorkspace, request.IdCategory, request.IdResponsible, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<ExpenseResponse>.Conflict(referenceCheck);

        var creatorId = _currentUser.UserId!.Value;

        try
        {
            var expense = new Expense(
                request.IdWorkspace,
                request.IdCategory,
                creatorId,
                request.IdResponsible,
                request.Name,
                request.Description,
                request.Cost,
                mappedStatus ?? request.Status,
                request.DatePay,
                request.DateCreate,
                request.DateUpdate);

            _dbContext.Expenses.Add(expense);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var created = await GetProjectionByIdAsync(expense.Id, cancellationToken);
            return ServiceResult<ExpenseResponse>.Success(MapToResponse(created!));
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

        var statusCheck = TryMapStatusKey(request.StatusKey, out var mappedStatus);
        if (!statusCheck)
            return ServiceResult<ExpenseResponse>.BadRequest("StatusKey must be one of: planned, pending_payment, paid, cancelled.");

        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null)
            return ServiceResult<ExpenseResponse>.NotFound("Expense was not found.");

        var access = await EnsureWorkspaceAccessAsync(expense.IdWorkspace, cancellationToken);
        if (!access.IsSuccess)
            return ToResult<ExpenseResponse>(access);

        if (request.IdWorkspace != expense.IdWorkspace)
            return ServiceResult<ExpenseResponse>.Forbidden("Expense cannot be moved to another workspace.");

        var referenceCheck = await ValidateReferencesAsync(expense.IdWorkspace, request.IdCategory, request.IdResponsible, cancellationToken);
        if (referenceCheck is not null)
            return ServiceResult<ExpenseResponse>.Conflict(referenceCheck);

        var status = mappedStatus ?? request.Status;

        try
        {
            _ = new Expense(
                expense.IdWorkspace,
                request.IdCategory,
                expense.IdCreator,
                request.IdResponsible,
                request.Name,
                request.Description,
                request.Cost,
                status,
                request.DatePay,
                request.DateCreate,
                request.DateUpdate);

            var entry = _dbContext.Entry(expense);
            entry.Property(x => x.IdCategory).CurrentValue = request.IdCategory;
            entry.Property(x => x.IdResponsible).CurrentValue = request.IdResponsible;
            entry.Property(x => x.Name).CurrentValue = request.Name;
            entry.Property(x => x.Description).CurrentValue = request.Description;
            entry.Property(x => x.Cost).CurrentValue = request.Cost;
            entry.Property(x => x.Status).CurrentValue = status;
            entry.Property(x => x.DatePay).CurrentValue = request.DatePay;
            entry.Property(x => x.DateCreate).CurrentValue = request.DateCreate;
            entry.Property(x => x.DateUpdate).CurrentValue = request.DateUpdate;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var updated = await GetProjectionByIdAsync(id, cancellationToken);
            return ServiceResult<ExpenseResponse>.Success(MapToResponse(updated!));
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

        var access = await EnsureWorkspaceAccessAsync(expense.IdWorkspace, cancellationToken);
        if (!access.IsSuccess)
            return access;

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

    private IQueryable<Expense> ApplyFilters(IQueryable<Expense> expenses, ExpenseListQuery query, int? mappedStatus)
    {
        var categoryId = query.CategoryId ?? query.IdCategory;
        var responsibleUserId = query.ResponsibleUserId ?? query.IdResponsible;

        if (query.IdWorkspace.HasValue)
            expenses = expenses.Where(x => x.IdWorkspace == query.IdWorkspace.Value);

        if (categoryId.HasValue)
            expenses = expenses.Where(x => x.IdCategory == categoryId.Value);

        if (query.IdCreator.HasValue)
            expenses = expenses.Where(x => x.IdCreator == query.IdCreator.Value);

        if (responsibleUserId.HasValue)
            expenses = expenses.Where(x => x.IdResponsible == responsibleUserId.Value);

        if (mappedStatus.HasValue)
            expenses = expenses.Where(x => x.Status == mappedStatus.Value);
        else if (query.Status.HasValue)
            expenses = expenses.Where(x => x.Status == query.Status.Value);

        if (query.DatePayFrom.HasValue)
            expenses = expenses.Where(x => x.DatePay >= query.DatePayFrom.Value);

        if (query.DatePayTo.HasValue)
            expenses = expenses.Where(x => x.DatePay <= query.DatePayTo.Value);

        if (query.DateCreateFrom.HasValue)
            expenses = expenses.Where(x => x.DateCreate >= query.DateCreateFrom.Value);

        if (query.DateCreateTo.HasValue)
            expenses = expenses.Where(x => x.DateCreate <= query.DateCreateTo.Value);

        if (query.AmountFrom.HasValue)
            expenses = expenses.Where(x => x.Cost.HasValue && x.Cost.Value >= query.AmountFrom.Value);

        if (query.AmountTo.HasValue)
            expenses = expenses.Where(x => x.Cost.HasValue && x.Cost.Value <= query.AmountTo.Value);

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

        return expenses;
    }

    private async Task<ExpenseProjection?> GetProjectionByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ExpenseProjection(
                x.Id,
                x.IdWorkspace,
                x.IdCategory,
                x.IdCreator,
                x.IdResponsible,
                x.Name,
                x.Description,
                x.Cost,
                x.Status,
                _dbContext.ExpenseCategories
                    .Where(category => x.IdCategory.HasValue && category.Id == x.IdCategory.Value)
                    .Select(category => category.Name)
                    .FirstOrDefault(),
                _dbContext.Workspaces
                    .Where(workspace => workspace.Id == x.IdWorkspace)
                    .Select(workspace => workspace.Name)
                    .FirstOrDefault() ?? string.Empty,
                _dbContext.Users
                    .Where(user => user.Id == x.IdCreator)
                    .Select(user => user.Login)
                    .FirstOrDefault() ?? string.Empty,
                _dbContext.Users
                    .Where(user => user.Id == x.IdCreator)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                _dbContext.Users
                    .Where(user => x.IdResponsible.HasValue && user.Id == x.IdResponsible.Value)
                    .Select(user => user.Login)
                    .FirstOrDefault(),
                _dbContext.Users
                    .Where(user => x.IdResponsible.HasValue && user.Id == x.IdResponsible.Value)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                x.DatePay,
                x.DateCreate,
                x.DateUpdate))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ServiceResult> EnsureWorkspaceAccessAsync(Guid? workspaceId, CancellationToken cancellationToken)
    {
        if (!workspaceId.HasValue || workspaceId.Value == Guid.Empty)
            return ServiceResult.BadRequest("Workspace id is required.");

        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult.Unauthorized("Authentication is required.");

        var exists = await _dbContext.Workspaces
            .AsNoTracking()
            .AnyAsync(x => x.Id == workspaceId.Value, cancellationToken);
        if (!exists)
            return ServiceResult.NotFound("Workspace was not found.");

        var hasAccess = await _dbContext.UserWorkspaces
            .AsNoTracking()
            .AnyAsync(x => x.IdWorkspace == workspaceId.Value && x.IdUser == _currentUser.UserId.Value, cancellationToken);

        return hasAccess
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("User does not have access to this workspace.");
    }

    private async Task<string?> ValidateReferencesAsync(
        Guid idWorkspace,
        Guid? idCategory,
        Guid? idResponsible,
        CancellationToken cancellationToken)
    {
        if (idCategory.HasValue)
        {
            var categoryExists = await _dbContext.ExpenseCategories.AsNoTracking().AnyAsync(x => x.Id == idCategory.Value, cancellationToken);
            if (!categoryExists)
                return "Expense category was not found.";
        }

        if (idResponsible.HasValue)
        {
            var responsibleExists = await _dbContext.UserWorkspaces
                .AsNoTracking()
                .AnyAsync(x => x.IdWorkspace == idWorkspace && x.IdUser == idResponsible.Value, cancellationToken);
            if (!responsibleExists)
                return "Responsible user must belong to the workspace.";
        }

        return null;
    }

    private static ServiceResult<T> ToResult<T>(ServiceResult result)
    {
        var error = result.Error!;
        return error.Type switch
        {
            ServiceErrorType.BadRequest => ServiceResult<T>.BadRequest(error.Message),
            ServiceErrorType.NotFound => ServiceResult<T>.NotFound(error.Message),
            ServiceErrorType.Conflict => ServiceResult<T>.Conflict(error.Message),
            ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(error.Message),
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Unavailable => ServiceResult<T>.Unavailable(error.Message),
            _ => ServiceResult<T>.Conflict(error.Message)
        };
    }

    private static ExpenseListItemResponse MapToListItemResponse(ExpenseProjection expense)
    {
        return new ExpenseListItemResponse(
            expense.Id,
            expense.IdWorkspace,
            expense.IdCategory,
            expense.IdCreator,
            expense.IdResponsible,
            expense.Name,
            expense.Cost,
            expense.Status,
            GetStatusKey(expense.Status),
            GetStatusLabel(expense.Status),
            expense.CategoryName,
            expense.WorkspaceName,
            expense.CreatorLogin,
            expense.CreatorEmail,
            expense.ResponsibleLogin,
            expense.ResponsibleEmail,
            expense.DatePay,
            expense.DateCreate,
            expense.DateUpdate);
    }

    private static ExpenseResponse MapToResponse(ExpenseProjection expense)
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
            GetStatusKey(expense.Status),
            GetStatusLabel(expense.Status),
            expense.CategoryName,
            expense.WorkspaceName,
            expense.CreatorLogin,
            expense.CreatorEmail,
            expense.ResponsibleLogin,
            expense.ResponsibleEmail,
            expense.DatePay,
            expense.DateCreate,
            expense.DateUpdate);
    }

    private static bool TryMapStatusKey(string? statusKey, out int? status)
    {
        status = statusKey?.Trim() switch
        {
            null or "" => null,
            "planned" => PlannedStatus,
            "pending_payment" => PendingPaymentStatus,
            "paid" => PaidStatus,
            "cancelled" => CancelledStatus,
            _ => null
        };

        return string.IsNullOrWhiteSpace(statusKey) || status.HasValue;
    }

    private static string? GetStatusKey(int status)
    {
        return status switch
        {
            PlannedStatus => "planned",
            PendingPaymentStatus => "pending_payment",
            PaidStatus => "paid",
            CancelledStatus => "cancelled",
            _ => null
        };
    }

    private static string GetStatusLabel(int status)
    {
        return status switch
        {
            PlannedStatus => "Запланирован",
            PendingPaymentStatus => "К оплате",
            PaidStatus => "Оплачен",
            CancelledStatus => "Отменён",
            _ => "Другой статус"
        };
    }

    private sealed record ExpenseProjection(
        Guid Id,
        Guid IdWorkspace,
        Guid? IdCategory,
        Guid IdCreator,
        Guid? IdResponsible,
        string Name,
        string? Description,
        decimal? Cost,
        int Status,
        string? CategoryName,
        string WorkspaceName,
        string CreatorLogin,
        string? CreatorEmail,
        string? ResponsibleLogin,
        string? ResponsibleEmail,
        DateTime? DatePay,
        DateTime DateCreate,
        DateTime DateUpdate);
}
